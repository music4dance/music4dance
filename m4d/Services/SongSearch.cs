using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace m4d.Services
{
    public class SongSearch
    {
        private SongFilter Filter { get; }
        private string UserName { get; }
        private bool IsPremium { get; }
        private UserManager<ApplicationUser> UserManager { get; }
        private SongIndex SongIndex { get; }
        private IBackgroundTaskQueue BackgroundTaskQueue { get; }
        public SongSearch(SongFilter filter, string userName, bool isPremium, SongIndex songIndex, UserManager<ApplicationUser> userManager, IBackgroundTaskQueue backgroundTaskQueue)
        {
            Filter = filter;
            UserName = userName;
            IsPremium = isPremium;
            SongIndex = songIndex;
            UserManager = userManager;
            BackgroundTaskQueue = backgroundTaskQueue;
        }

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
                    var temp = await UserMapper.AnonymizeFilter(Filter, UserManager);
                    Filter.User = temp.User;
                }
            }

            var p = SongIndex.AzureParmsFromFilter(
                await UserMapper.DeanonymizeFilter(Filter, UserManager));

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

            return new SearchResults(results, songs.Skip(offset).Take(25).ToList(),songs.Count);
        }

        private async Task LogSearch(SongFilter filter)
        {
            if (filter.IsEmptyUser(UserName) || filter.Action == "holidaymusic")
            {
                return;
            }

            var filterString = filter.Normalize(UserName).ToString();

            var user = IsAuthenticated
                ? await UserManager.FindByNameAsync(UserName)
                : null;
            var userId = user?.Id;

            var now = DateTime.Now;

            BackgroundTaskQueue.EnqueueTask(
                async (serviceScopeFactory, cancellationToken) =>
                {
                    try
                    {
                        using var scope = serviceScopeFactory.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();

                        var old = await context.Searches.FirstOrDefaultAsync(
                            s => s.ApplicationUserId == userId && s.Query == filterString);

                        if (old != null)
                        {
                            old.Modified = now;
                            old.Count += 1;
                        }
                        else
                        {
                            await context.Searches.AddAsync(
                                new()
                                {
                                    ApplicationUserId = userId,
                                    Query = filterString,
                                    Count = 1,
                                    Created = now,
                                    Modified = now,
                                }, cancellationToken);
                        }

                        await context.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.Message);
                    }
                });
        }

    }
}
