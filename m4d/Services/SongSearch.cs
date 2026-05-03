using Azure.Search.Documents;

using m4d.Services.ServiceHealth;
using m4d.Utilities;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using System.Diagnostics;

namespace m4d.Services;

public class SongSearch(SongFilter filter, string userName, bool isPremium, SongIndex songIndex,
    UserManager<ApplicationUser> userManager, IBackgroundTaskQueue backgroundTaskQueue,
    ServiceHealthManager serviceHealth = null, int? pageSize = null, bool isAdmin = false)
{
    private SongFilter Filter { get; } = songIndex.DanceMusicService.SearchService.GetSongFilter(filter.ToString());
    private string UserName { get; } = userName;
    private bool IsPremium { get; } = isPremium;
    private bool IsAdmin { get; } = isAdmin;
    private UserManager<ApplicationUser> UserManager { get; } = userManager;
    private SongIndex SongIndex { get; } = songIndex;
    private IBackgroundTaskQueue BackgroundTaskQueue { get; } = backgroundTaskQueue;
    private ServiceHealthManager ServiceHealth { get; } = serviceHealth;
    private int? PageSize { get; } = pageSize;

    private bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserName);

    public async Task<SearchResults> Search()
    {
        if (Filter.Level != null && Filter.Level != 0 && !IsPremium)
        {
            throw new RedirectException("RequiresPremium");
        }

        var userQuery = Filter.UserQuery;
        var currentUser = UserName;
        if (!userQuery.IsEmpty)
        {
            if (userQuery.IsIdentity)
            {
                if (IsAuthenticated)
                {
                    Filter.User = new UserQuery(userQuery, currentUser).Query;
                }
                else
                {
                    throw new RedirectException("Login", Filter);
                }
            }
            else if (!string.Equals(currentUser, userQuery.UserName))
            {
                // In this case we want to intentionally overwrite the incoming filter,
                // unless the requesting user is an admin — admins can view any user's songs
                // regardless of that user's privacy setting.
                if (!IsAdmin)
                {
                    var temp = await UserMapper.AnonymizeFilter(Filter, UserManager, ServiceHealth);
                    Filter.User = temp.User;
                }
            }
        }

        var p = SongIndex.AzureParmsFromFilter(
            await UserMapper.DeanonymizeFilter(Filter, UserManager, ServiceHealth), PageSize);

        p.IncludeTotalCount = true;

        await LogSearch(Filter);

        if (userQuery.IsVoted && Filter.DanceQuery.Dances.Any())
        {
            return await VoteSearch(p);
        }

        try
        {
            return await SongIndex.Search(
                Filter.SearchString, p, Filter.CruftFilter);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Search service is unavailable") ||
                                                   ex.Message.Contains("Client registration requires a TokenCredential"))
        {
            ServiceHealth?.MarkUnavailable("SearchService", $"Client error: {ex.Message}");
            return new SearchResults(Filter.SearchString ?? "", 0, 0, 1, PageSize ?? 25, [], new Dictionary<string, IList<Azure.Search.Documents.Models.FacetResult>>());
        }
    }

    public async Task<SearchResults> VoteSearch(SearchOptions options)
    {
        var userQuery = Filter.UserQuery;
        var vote = userQuery.IsUpVoted ? 1 : -1;
        var user = userQuery.IsIdentity ? UserName : userQuery.UserName;
        return await PostSearch(options,
            s => Filter.DanceQuery.Dances.Any(d => s.NormalizedUserDanceRating(user, d.Id) == vote));
    }

    /// <summary>
    /// Returns songs where at least one edit block is attributed to <paramref name="editorUser"/>
    /// and whose timestamp falls within [<paramref name="from"/>, <paramref name="to"/>].
    /// Uses <see cref="PostSearch"/> which streams all matching songs from Azure Search via
    /// <see cref="SongIndex.StreamAll"/> and applies the filter in memory.
    /// </summary>
    public async Task<SearchResults> EditedBySearch(SearchOptions options, string editorUser,
        DateTime from, DateTime to)
    {
        return await PostSearch(options, s => s.WasEditedBy(editorUser, from, to));
    }

    /// <summary>
    /// Streams all songs from Azure Search via <see cref="SongIndex.StreamAll"/> and applies
    /// <paramref name="predicate"/> as an in-memory post-filter. Non-matching songs are released
    /// page-by-page (only one Azure page of 1000 songs transits memory at a time), but every song
    /// that satisfies the predicate accumulates in a list for the duration of the call. Peak memory
    /// scales with the number of matches, not the total Azure result set. Each page request
    /// re-scans from the beginning; there is no server-side cursor between pages.
    /// </summary>
    private async Task<SearchResults> PostSearch(SearchOptions options, Func<Song, bool> predicate)
    {
        var offset = options.Skip ?? 0;
        var pageSize = options.Size ?? PageSize ?? 25;

        var matched = new List<Song>();
        try
        {
            await foreach (var song in SongIndex.StreamAll(
                Filter.SearchString, options, Filter.CruftFilter))
            {
                if (predicate(song)) matched.Add(song);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Search service is unavailable") ||
                                                   ex.Message.Contains("Client registration requires a TokenCredential"))
        {
            ServiceHealth?.MarkUnavailable("SearchService", $"Client error: {ex.Message}");
            return new SearchResults(Filter.SearchString ?? "", 0, 0, 1, pageSize, [], new Dictionary<string, IList<Azure.Search.Documents.Models.FacetResult>>());
        }

        var page = matched.Skip(offset).Take(pageSize).ToList();
        return new SearchResults(
            Filter.SearchString ?? "",
            page.Count,
            matched.Count,
            offset / pageSize + 1,
            pageSize,
            page,
            new Dictionary<string, IList<Azure.Search.Documents.Models.FacetResult>>());
    }

    public void Page()
    {
        if (Filter.Page.HasValue)
        {
            Filter.Page += 1;
        }
        else
        {
            Filter.Page = 2;
        }
    }

    internal async Task LogSearch(SongFilter filter)
    {
        if (filter.IsEmptyUser(UserName) || filter.Action == "customsearch")
        {
            return;
        }

        var filterString = filter.Normalize(UserName).ToString();

        var user = IsAuthenticated
            ? await UserManager.FindByNameAsync(UserName)
            : null;
        var userId = user?.Id;

        var now = DateTime.Now;

        var mostRecentPage = (filter.Page.HasValue && filter.Page.Value > 1) ? filter.Page : null;

        // Skip logging if database is unavailable
        if (ServiceHealth?.IsServiceHealthy("Database") == false)
        {
            return;
        }

        BackgroundTaskQueue.EnqueueTask(
            async (serviceScopeFactory, cancellationToken) =>
            {
                try
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();

                    var old = await context.Searches.FirstOrDefaultAsync(s => s.ApplicationUserId == userId && s.Query == filterString, cancellationToken: cancellationToken);

                    if (old != null)
                    {
                        old.Modified = now;
                        old.Count += 1;
                        if (userId != null)
                        {
                            old.MostRecentPage = mostRecentPage;
                        }
                    }
                    else
                    {
                        _ = await context.Searches.AddAsync(
                            new()
                            {
                                ApplicationUserId = userId,
                                Query = filterString,
                                Count = 1,
                                Created = now,
                                Modified = now,
                                MostRecentPage = userId != null ? mostRecentPage : null,
                            }, cancellationToken);
                    }

                    _ = await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            });
    }

}
