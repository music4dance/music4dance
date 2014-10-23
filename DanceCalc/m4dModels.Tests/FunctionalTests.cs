using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using m4dModels;

namespace m4dModels.Tests
{
    [TestClass]
    public class FunctionalTests
    {
        static FunctionalTests()
        {
            string dir = System.Environment.CurrentDirectory;
            Trace.WriteLine(dir);
            s_users = File.ReadAllLines(@".\TestData\test-users.txt").ToList();
            s_tags = File.ReadAllLines(@".\TestData\test-tags.txt").ToList();
            s_songs = File.ReadAllLines(@".\TestData\test-songs.txt").ToList();

            s_dms.LoadUsers(s_users);
            s_dms.LoadTags(s_tags);
            s_dms.LoadSongs(s_songs);
        }

        [TestMethod]
        public void LoadDatabase()
        {
            var users = from u in s_dms.Context.Users select u;
            Assert.AreEqual(s_users.Count() - 1, users.Count(),"Count of Users");
            var tts = from tt in s_dms.Context.TagTypes select tt;
            Assert.AreEqual(s_tags.Count(), tts.Count(), "Count of Tag Types");
            var songs = from s in s_dms.Context.Songs where s.TitleHash != 0 select s;
            Assert.AreEqual(s_songs.Count(), s_dms.Songs.Count(),"Count of Songs");
        }

        [TestMethod]
        public void SaveDatabase()
        {
            IList<string> songs = s_dms.SerializeSongs(false,true);
            Assert.IsTrue(ListEquivalent(s_songs, songs));

            IList<string> tags = s_dms.SerializeTags(false);
            Assert.IsTrue(ListEquivalent(s_tags, tags));

            // TODONEXT: To get this to work, we have to add in roles to the Mock Context.
            //IList<string> users = s_dms.SerializeUsers(true);
            //Assert.IsTrue(ListEquivalent(s_users, users));
        }

        static bool ListEquivalent(IList<string> expected, IList<string> actual)
        {
            List<string> expectedExtra = new List<string>();

            foreach (string e in expected)
            {
                int i = actual.IndexOf(e);
                if (i == -1)
                {
                    expectedExtra.Add(e);
                }
                else
                {
                    actual.RemoveAt(i);
                }
            }

            if (expectedExtra.Count > 0)
            {
                Trace.WriteLine("Expected Extra:\r\n");
                foreach (string s in expectedExtra)
                {
                    Trace.WriteLine(s);
                }
            }
            if (actual.Count > 0)
            {
                Trace.WriteLine("Actual Extra:\r\n");
                foreach (string s in actual)
                {
                    Trace.WriteLine(s);
                }
            }

            return actual.Count == 0 && expectedExtra.Count == 0;
        }
        static List<string> s_users;
        static List<string> s_tags;
        static List<string> s_songs;

        static DanceMusicService s_dms = new DanceMusicService(new MockContext(false));
    }
}
