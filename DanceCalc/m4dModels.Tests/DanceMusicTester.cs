using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace m4dModels.Tests
{
    public class DanceMusicTester
    {
        public DanceMusicTester(List<string> songs = null)
        {
            Dms = new DanceMusicService(new MockContext(false));

            Dms.SeedDances();

            var dir = System.Environment.CurrentDirectory;
            Trace.WriteLine(dir);
            Users = File.ReadAllLines(@".\TestData\test-users.txt").ToList();
            Dances = File.ReadAllLines(@".\TestData\test-dances.txt").ToList();
            Tags = File.ReadAllLines(@".\TestData\test-tags.txt").ToList();
            Songs = songs ?? File.ReadAllLines(@".\TestData\test-songs.txt").ToList();

           Dms.LoadUsers(Users);
           Dms.LoadDances(Dances);
           Dms.LoadTags(Tags);
           Dms.LoadSongs(Songs);
        }

        public List<string> Users { private set; get; }

        public List<string> Dances { private set; get; }

        public List<string> Tags { private set; get; }

        public List<string> Songs { private set; get; }

        public DanceMusicService Dms { private set; get; }

        public static string ReplaceTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }
            else
            {
                Regex r = new Regex("\tTime=[^\t]*");
                return r.Replace(s, "Time=00/00/0000 0:00:00 PM");
            }
        }

    }
}