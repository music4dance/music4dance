using Azure.Search.Documents;

using m4d.Services.ServiceHealth;
using m4d.Utilities;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using System.Diagnostics;

namespace m4d.Services;

public class SongSearch(SongFilter filter, string userName, bool isPremium, SongIndex songIndex,
    UserManager<ApplicationUser> userManager, IBackgroundTaskQueue backgroundTaskQueue,
    ServiceHealthManager serviceHealth = null, int? pageSize = null)
{
    private SongFilter Filter { get; } = songIndex.DanceMusicService.SearchService.GetSongFilter(filter.ToString());
    private string UserName { get; } = userName;
    private bool IsPremium { get; } = isPremium;
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
                // In this case we want to intentionally overwrite the incoming filter
                var temp = await UserMapper.AnonymizeFilter(Filter, UserManager, ServiceHealth);
                Filter.User = temp.User;
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

        return await SongIndex.Search(
            Filter.SearchString, p, Filter.CruftFilter);
    }

    // TODO:
    //  - Think about how to handle additional filter - continue down the path
    //    of truncation or move towards infinite scrolling
    //  - Can we use facets to get user's pages to have links to dance lists (no)
    public async Task<SearchResults> VoteSearch(SearchOptions options)
    {
        var offset = options.Skip ?? 0;
        options.Skip = 0;
        options.Size = 500;
        var results = await SongIndex.Search(
            Filter.SearchString, options, Filter.CruftFilter);

        var userQuery = Filter.UserQuery;
        var vote = userQuery.IsUpVoted ? 1 : -1;
        var user = userQuery.IsIdentity ? UserName : userQuery.UserName;
        var songs = results.Songs.Where(s => Filter.DanceQuery.Dances.Any(d => s.NormalizedUserDanceRating(user, d.Id) == vote)).ToList();
        var negated = results.Songs.Where(s => Filter.DanceQuery.Dances.All(d => s.NormalizedUserDanceRating(user, d.Id) != vote)).ToList();

        return new SearchResults(results, [.. songs.Skip(offset).Take(options.Size ?? 25)], songs.Count);
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

    private async Task LogSearch(SongFilter filter)
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
