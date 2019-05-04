using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    public class DanceMusicTester
    {
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

        public DanceMusicTester(List<string> songs = null)
        {
            Dms = MockContext.CreateService(false);

            Dms.SeedDances();

            var dir = Environment.CurrentDirectory;
            Trace.WriteLine(dir);
            Users = ReadResource("test-users.txt");
            Dances = ReadResource(@"test-dances.txt");
            Tags = ReadResource(@"test-tags.txt");
            Searches = ReadResource(@"test-searches.txt");

            Dms.LoadUsers(Users);
            Dms.LoadDances(Dances);
            Dms.LoadTags(Tags);
            Dms.LoadSearches(Searches);
        }

        public List<string> Users { private set; get; }

        public List<string> Dances { private set; get; }

        public List<string> Tags { private set; get; }

        public List<string> Searches { private set; get; }

        public DanceMusicService Dms { private set; get; }

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

                Trace.WriteLine("Failed at " + i + "[" + a.Substring(0,i) + "]" );
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
    }
}