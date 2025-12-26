using m4d.Services.ServiceHealth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace m4d.Utilities;

public static class UserMapper
{
    private static readonly Dictionary<string, UserInfo> s_cachedUsers =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, UserInfo> s_cachedIds =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly SemaphoreSlim s_semaphore = new(1, 1);

    private static DateTime CacheTime { get; set; }

    public static async Task<IReadOnlyDictionary<string, UserInfo>> GetUserNameDictionary(
        UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        await BuildDictionaries(userManager, serviceHealth);
        return s_cachedUsers;
    }

    public static async Task<List<UserInfo>> GetPremiumUsers(
        UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        var dict = await GetUserNameDictionary(userManager, serviceHealth);

        return [.. dict.Values.Where(u =>
            u.Roles.Contains(DanceMusicCoreService.PremiumRole) ||
            u.User.LifetimePurchased > 0)];
    }

    public static async Task<IReadOnlyDictionary<string, UserInfo>> GetUserIdDictionary(
        UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        await BuildDictionaries(userManager, serviceHealth);
        return s_cachedIds;
    }

    public static void Clear()
    {
        s_cachedUsers.Clear();
        s_cachedIds.Clear();
        CacheTime = DateTime.MinValue;
    }

    public static async Task<SongFilter> DeanonymizeFilter(
        SongFilter filter, UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        var dictionary = await GetUserIdDictionary(userManager, serviceHealth);
        var userName = filter.UserQuery.UserName;
        if (userName == null)
        {
            return filter;
        }
        var realName = Deanonymize(userName, dictionary);
        if (!string.Equals(userName, realName, StringComparison.InvariantCultureIgnoreCase))
        {
            filter = filter.Clone();
            filter.User = filter.User.Replace(userName, realName);
        }

        return filter;
    }

    public static async Task<SongFilter> AnonymizeFilter(
        SongFilter filter, UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        var dictionary = await GetUserNameDictionary(userManager, serviceHealth);
        var userName = filter.UserQuery.UserName;
        if (userName == null)
        {
            return filter;
        }
        var anonName = Anonymize(userName, dictionary);
        if (!string.Equals(userName, anonName, StringComparison.InvariantCultureIgnoreCase))
        {
            filter = filter.Clone();
            filter.User = filter.User.Replace(userName, anonName, StringComparison.InvariantCultureIgnoreCase);
        }

        return filter;
    }

    public static async Task<SongHistory> AnonymizeHistory(
        SongHistory history, UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        var cache = await GetUserNameDictionary(userManager, serviceHealth);

        return ModifyHistory(history, p => Anonymize(p, cache));
    }

    public static SongHistory AnonymizeHistory(
        SongHistory history, IReadOnlyDictionary<string, UserInfo> dictionary)
    {
        return ModifyHistory(history, p => Anonymize(p, dictionary));
    }

    public static async Task<SongHistory> DeanonymizeHistory(
        SongHistory history, UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        var cache = await GetUserIdDictionary(userManager, serviceHealth);

        return ModifyHistory(history, p => Deanonymize(p, cache));
    }

    public static SongHistory DeanonymizeHistory(
        SongHistory history, IReadOnlyDictionary<string, UserInfo> dictionary)
    {
        return ModifyHistory(history, p => Deanonymize(p, dictionary));
    }

    private static SongHistory ModifyHistory(SongHistory history,
        Func<string, string> transform)
    {
        return new()
        {
            Id = history.Id,
            Properties = [.. history.Properties.Select(
                p => p.Name == Song.UserField
                    ? new SongPropertySparse
                    {
                        Name = p.Name,
                        Value = transform(p.Value)
                    }
                    : p
            )],
        };
    }

    private static string Anonymize(string userName,
        IReadOnlyDictionary<string, UserInfo> dictionary)
    {
        if (dictionary.TryGetValue(userName, out var user))
        {
            // Privacy == 0 means maximum privacy requested (anonymize to user ID)
            // Privacy > 0 means public (keep username visible)
            if (user.User.Privacy == 0)
            {
                return user.User.Id;
            }
            return userName;
        }

        // User not in dictionary - could be because DB is unavailable or user doesn't exist
        // When dictionary is empty (DB down with no cache), anonymize by returning placeholder
        // When dictionary has data but user not found, it's likely already an anonymized ID
        if (dictionary.Count == 0)
        {
            // DB unavailable with no cache - hide username for privacy
            return "*UNAVAILABLE*";
        }

        // User not in dictionary but dictionary has data - keep current value
        // (it may already be an anonymized user ID from previous operations)
        return userName;
    }

    private static string Deanonymize(string id,
        IReadOnlyDictionary<string, UserInfo> dictionary)
    {
        if (dictionary.TryGetValue(id, out var user))
        {
            // Privacy == 0 means maximum privacy requested (keep as ID)
            // Privacy > 0 means public (deanonymize to username)
            if (user.User.Privacy > 0)
            {
                return user.User.UserName;
            }
            return id;
        }

        // User not in dictionary (database unavailable) - keep current value
        return id;
    }


    // TODO:  implment AsyncLock class https://www.hanselman.com/blog/comparing-two-techniques-in-net-asynchronous-coordination-primitives
    private static async Task BuildDictionaries(UserManager<ApplicationUser> userManager, ServiceHealthManager serviceHealth = null)
    {
        await s_semaphore.WaitAsync();
        try
        {
            // Check if database is available
            if (serviceHealth != null)
            {
                var isDatabaseAvailable = serviceHealth.IsServiceHealthy("Database");

                if (!isDatabaseAvailable)
                {
                    // Keep stale cache if we have it, otherwise users will show as IDs
                    Console.WriteLine("Database unavailable - using stale user cache if available");
                    return;
                }
            }

            // Only rebuild if cache is empty
            if (s_cachedUsers.Count == 0)
            {
                await InternalBuildDictionaries(userManager);
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            // Fallback for when serviceHealth wasn't provided but database is actually down
            Console.WriteLine($"WARNING: Unable to load user information (database unavailable): {ex.Message}");
            Console.WriteLine("Using stale user cache if available, otherwise users will appear as IDs");
            // Keep whatever cache we have (might be empty, might be stale)
        }
        finally
        {
            _ = s_semaphore.Release();
        }
    }

    private static async Task InternalBuildDictionaries(UserManager<ApplicationUser> userManager)
    {
        foreach (var user in userManager.Users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var logins = await userManager.GetLoginsAsync(user);

            var userInfo = new UserInfo
            {
                User = user,
                Roles = [.. roles],
                Logins = [.. logins.Select(l => l.LoginProvider)]
            };

            s_cachedUsers.Add(user.UserName, userInfo);
            s_cachedIds.Add(user.Id, userInfo);
        }

        CacheTime = DateTime.Now;
    }
}
