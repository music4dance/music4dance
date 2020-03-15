using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{

    class UserLogger : ILogger<UserManager<ApplicationUser>>
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel == LogLevel.Trace;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine($@"Log: {logLevel}; exception = {exception}; ");
        }
    }

    class RoleLogger : ILogger<RoleManager<IdentityRole>>
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel == LogLevel.Trace;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine($@"Log: {logLevel}; exception = {exception}; ");
        }
    }

    public class DanceMusicTester
    {
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
                if (a[i] == b[i]) continue;

                Trace.WriteLine("Failed at " + i + "[" + a.Substring(0, i) + "]");
                return false;
            }

            if (a.Length <= b.Length) return b.Length <= a.Length;

            return false;
        }

        public static void DumpSongProperties(Song song, bool verbose = true)
        {
            if (!verbose) return;

            foreach (var prop in song.SongProperties)
            {
                Trace.WriteLine(prop.ToString());
            }
        }

        public static DanceStatsInstance GetDanceStats(DanceStatsManager manager = null)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith("dancestatistics.txt"));

            string json;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                Assert.IsNotNull(stream);
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }

            var instance = manager == null ? DanceStatsInstance.LoadFromJson(json) : manager.LoadFromJson(json);
            Assert.IsNotNull(instance);

            return instance;
        }

        public static DanceMusicService CreateService(string name)
        {
            var contextOptions = new DbContextOptionsBuilder<DanceMusicContext>()
                .UseInMemoryDatabase(databaseName: name).Options;

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
                new PasswordValidator<ApplicationUser>()
            };

            var identityErrorDescriber = new IdentityErrorDescriber();
            var invariantLookupNormalizer = new UpperInvariantLookupNormalizer();

            var options = Options.Create(identityOption);
            var userManager = new UserManager<ApplicationUser>(
                new UserStore<ApplicationUser>(context),
                options,
                new PasswordHasher<ApplicationUser>(),
                new List<IUserValidator<ApplicationUser>>(),
                pwdValidators,
                invariantLookupNormalizer,
                identityErrorDescriber,
                null,
                new UserLogger()
            );

            var roleManager = new RoleManager<IdentityRole>(
                new RoleStore<IdentityRole>(context),
                new List<IRoleValidator<IdentityRole>>(),
                invariantLookupNormalizer,
                identityErrorDescriber,
                new RoleLogger()
            );

            var manager = new DanceStatsManager(null);
            GetDanceStats(manager);

            var service = new DanceMusicService(context, userManager, null, manager);

            SeedRoles(roleManager);

            return service;
        }


        public static DanceMusicService CreateServiceWithUsers(string name)
        {
            var service = CreateService(name);

            service.FindOrAddUser("dwgray");
            service.FindOrAddUser("batch");
            service.FindOrAddUser("batch-a");
            service.FindOrAddUser("batch-e");
            service.FindOrAddUser("batch-i");
            service.FindOrAddUser("batch-s");
            service.FindOrAddUser("batch-x");
            service.FindOrAddUser("DWTS");
            service.FindOrAddUser("Charlie");
            service.FindOrAddUser("ohdwg");

            return service;
        }


        public static DanceMusicService CreatePopulatedService(string name)
        {
            var service = CreateService(name);

            var users = ReadResource("test-users.txt");
            var dances = ReadResource(@"test-dances.txt");
            var tags = ReadResource(@"test-tags.txt");
            var searches = ReadResource(@"test-searches.txt");

            service.LoadUsers(users);
            service.LoadDances(dances);
            service.LoadTags(tags);
            service.LoadSearches(searches);

            return service;
        }

        private static List<string> ReadResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(name));

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd().Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        private static void SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            foreach (var roleName in DanceMusicService.Roles)
            {
                if (!roleManager.RoleExistsAsync(roleName).Result)
                {
                    var result = roleManager.CreateAsync(new IdentityRole { Name = roleName }).Result;
                }
            }
        }

    }
}
