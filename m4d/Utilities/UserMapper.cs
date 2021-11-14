using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;

namespace m4d.Utilities
{
    public static class UserMapper
    {
        private static readonly Dictionary<string, UserInfo> s_cachedUsers =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, UserInfo> s_cachedIds =
            new(StringComparer.OrdinalIgnoreCase);

        private static DateTime CacheTime { get; set; }

        public static async Task<IReadOnlyDictionary<string, UserInfo>> GetUserNameDictionary(
            UserManager<ApplicationUser> userManager)
        {
            if (s_cachedUsers.Count == 0)
            {
                await BuildDictionaries(userManager);
            }

            return s_cachedUsers;
        }

        public static async Task<IReadOnlyDictionary<string, UserInfo>> GetUserIdDictionary(
            UserManager<ApplicationUser> userManager)
        {
            if (s_cachedUsers.Count == 0)
            {
                await BuildDictionaries(userManager);
            }

            return s_cachedIds;
        }

        public static void Clear()
        {
            s_cachedUsers.Clear();
            CacheTime = DateTime.MinValue;
        }

        public static async Task<SongFilter> DeanonymizeFilter(
            SongFilter filter, UserManager<ApplicationUser> userManager)
        {
            var dictionary = await GetUserIdDictionary(userManager);
            var userName = filter.UserQuery.UserName;
            var realName = Deanonymize(userName, dictionary);
            if (!string.Equals(userName, realName, StringComparison.InvariantCultureIgnoreCase))
            {
                filter = new SongFilter(filter.ToString());
                filter.User = filter.User.Replace(userName, realName);
            }

            return filter;
        }

        public static async Task<SongHistory> AnonymizeHistory(
            SongHistory history, UserManager<ApplicationUser> userManager)
        {
            var cache = await GetUserNameDictionary(userManager);

            return ModifyHistory(history, p => Anonymize(p, cache));
        }

        public static SongHistory AnonymizeHistory(
            SongHistory history, IReadOnlyDictionary<string, UserInfo> dictionary)
        {
            return ModifyHistory(history, p => Anonymize(p, dictionary));
        }

        public static async Task<SongHistory> DeanonymizeHistory(
            SongHistory history, UserManager<ApplicationUser> userManager)
        {
            var cache = await GetUserIdDictionary(userManager);

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
                Properties = history.Properties.Select(
                    p => p.Name == Song.UserField
                        ? new SongPropertySparse
                        {
                            Name = p.Name,
                            Value = transform(p.Value)
                        }
                        : p
                ).ToList(),
            };
        }

        private static string Anonymize(string userName,
            IReadOnlyDictionary<string, UserInfo> dictionary)
        {
            if (dictionary.TryGetValue(userName, out var user) && user.User.Privacy == 0)
            {
                return user.User.Id;
            }

            return userName;
        }

        private static string Deanonymize(string id,
            IReadOnlyDictionary<string, UserInfo> dictionary)
        {
            if (dictionary.TryGetValue(id, out var user) && user.User.Privacy == 0)
            {
                return user.User.UserName;
            }

            return id;
        }

        private static async Task BuildDictionaries(UserManager<ApplicationUser> userManager)
        {
            foreach (var user in userManager.Users)
            {
                var roles = await userManager.GetRolesAsync(user);
                var logins = await userManager.GetLoginsAsync(user);

                var userInfo = new UserInfo
                {
                    User = user,
                    Roles = roles.ToList(),
                    Logins = logins.Select(l => l.LoginProvider).ToList()
                };

                s_cachedUsers.Add(user.UserName, userInfo);
                s_cachedIds.Add(user.Id, userInfo);
            }

            CacheTime = DateTime.Now;
        }
    }
}
