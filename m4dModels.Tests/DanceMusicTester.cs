using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace m4dModels.Tests
{
    internal class UserLogger : ILogger<UserManager<ApplicationUser>>
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel == LogLevel.Trace;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine($@"Log: {logLevel}; exception = {exception}; ");
        }
    }

    internal class RoleLogger : ILogger<RoleManager<IdentityRole>>
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel == LogLevel.Trace;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine($@"Log: {logLevel}; exception = {exception}; ");
        }
    }

    public class TestDSFileManager : IDanceStatsFileManager
    {
        public Task<string> GetDances()
        {
            return DanceMusicTester.ReadResourceFile("test-dances.json");
        }

        public Task<string> GetGroups()
        {
            return DanceMusicTester.ReadResourceFile("test-groups.json");
        }

        public Task<string> GetStats()
        {
            return DanceMusicTester.ReadResourceFile("dancestatistics.txt");
        }

        public Task WriteStats(string stats)
        {
            throw new NotImplementedException();
        }
    }

    public static class DanceMusicTester
    {
        public static async Task<bool> LoadDances()
        {
            var files = new TestDSFileManager();
            DanceLibrary.Dances.Reset(
                DanceLibrary.Dances.Load(
                    await files.GetDances(), await files.GetGroups()));
            return true;
        }

        public static string ReplaceTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            var r = new Regex("\tTime=[^\t]*");
            return r.Replace(s, "\tTime=00/00/0000 0:00:00 PM");
        }

        public static bool CompareStrings(string a, string b)
        {
            var length = Math.Min(a.Length, b.Length);
            for (var i = 0; i < length; i++)
            {
                if (a[i] == b[i])
                {
                    continue;
                }

                Trace.WriteLine("Failed at " + i + "[" + a[..i] + "]");
                return false;
            }

            return a.Length <= b.Length && b.Length <= a.Length;
        }

        public static void DumpSongProperties(Song song, bool verbose = true)
        {
            if (!verbose)
            {
                return;
            }

            foreach (var prop in song.SongProperties)
            {
                Trace.WriteLine(prop.ToString());
            }
        }

        public static async Task<DanceMusicService> CreateService(string name)
        {
            var contextOptions = new DbContextOptionsBuilder<DanceMusicContext>()
                .UseInMemoryDatabase(name).Options;

            var context = new DanceMusicContext(contextOptions);

            var identityOption = new IdentityOptions
            {
                Password =
                {
                    RequiredLength = 8,
                    RequireNonAlphanumeric = true,
                    RequireDigit = true,
                    RequireUppercase = true
                }
            };


            var pwdValidators = new List<PasswordValidator<ApplicationUser>>
            {
                new()
            };

            var identityErrorDescriber = new IdentityErrorDescriber();
            var invariantLookupNormalizer = new UpperInvariantLookupNormalizer();

            var options = Options.Create(identityOption);
            var userManager = new UserManager<ApplicationUser>(
                new UserStore<ApplicationUser>(context),
                options,
                new PasswordHasher<ApplicationUser>(),
                [],
                pwdValidators,
                invariantLookupNormalizer,
                identityErrorDescriber,
                null,
                new UserLogger()
            );

            var roleManager = new RoleManager<IdentityRole>(
                new RoleStore<IdentityRole>(context),
                [],
                invariantLookupNormalizer,
                identityErrorDescriber,
                new RoleLogger()
            );

            var manager = new DanceStatsManager(new TestDSFileManager());

            var songIndex = new Mock<FlatSongIndex>();
            var service = new DanceMusicService(context, userManager, null, manager, songIndex.Object);
            songIndex.Setup(m => m.UpdateIndex(new List<string>())).ReturnsAsync(true);
            songIndex.Setup(m => m.DanceMusicService).Returns(service);
            await manager.Initialize(service);
            songIndex.Setup(m => m.DanceMusicService).Returns(service);
            await manager.Instance.FixupStats(service, false);

            await SeedRoles(roleManager);

            return service;
        }


        public static async Task<DanceMusicService> CreateServiceWithUsers(string name)
        {
            var service = await CreateService(name);

            await AddUser(service, "dwgray", false);
            await AddUser(service, "batch", true);
            await AddUser(service, "batch-a", true);
            await AddUser(service, "batch-e", true);
            await AddUser(service, "batch-i", true);
            await AddUser(service, "batch-s", true);
            await AddUser(service, "batch-x", true);
            await AddUser(service, "DWTS", true);
            await AddUser(service, "Charlie", false);
            await AddUser(service, "ohdwg", false);
            await AddUser(service, "UserG", true);
            await AddUser(service, "UserAC", true);
            await AddUser(service, "UserN", true);
            await AddUser(service, "UserZ", true);
            await AddUser(service, "UserF", true);
            return service;
        }

        private static async Task AddUser(DanceMusicService service, string name, bool pseudo)
        {
            await service.FindOrAddUser(
                name,
                pseudo ? DanceMusicCoreService.PseudoRole : DanceMusicCoreService.EditRole,
                pseudo ? null : $"{name}@hotmail.com");
        }

        public static async Task<DanceMusicService> CreatePopulatedService(string name)
        {
            // TODO: Should be able to make these happen in parallel
            var service = await CreateService(name);

            var users = await ReadResourceList("test-users-clean.txt");
            var dances = await ReadResourceList(@"test-dances.txt");
            var tags = await ReadResourceList(@"test-tags.txt");
            var searches = await ReadResourceList(@"test-searches.txt");

            await service.LoadUsers(users);
            await service.LoadDances(dances);
            await service.LoadTags(tags);
            await service.LoadSearches(searches);

            return service;
        }

        internal static async Task<string> ReadResourceFile(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(name));

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        internal static async Task<List<string>> ReadResourceList(string name)
        {
            var text = await ReadResourceFile(name);
            return [.. text.Split(
                Environment.NewLine.ToCharArray(),
                StringSplitOptions.RemoveEmptyEntries)];
        }

        private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            foreach (var roleName in DanceMusicCoreService.Roles)
            {
                if (!roleManager.RoleExistsAsync(roleName).Result)
                {
                    await roleManager.CreateAsync(new IdentityRole { Name = roleName });
                }
            }
        }
    }
}
