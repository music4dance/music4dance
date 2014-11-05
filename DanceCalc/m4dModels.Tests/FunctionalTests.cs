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
            s_dms.SeedDances();

            string dir = System.Environment.CurrentDirectory;
            Trace.WriteLine(dir);
            s_users = File.ReadAllLines(@".\TestData\test-users.txt").ToList();
            s_dances = File.ReadAllLines(@".\TestData\test-dances.txt").ToList();
            s_tags = File.ReadAllLines(@".\TestData\test-tags.txt").ToList();
            s_songs = File.ReadAllLines(@".\TestData\test-songs.txt").ToList();

            s_dms.LoadUsers(s_users);
            s_dms.LoadDances(s_dances);
            s_dms.LoadTags(s_tags);
            s_dms.LoadSongs(s_songs);
        }

        [TestMethod]
        public void LoadDatabase()
        {
            var users = from u in s_dms.Context.Users select u;
            Assert.AreEqual(s_users.Count() - 1, users.Count(),"Count of Users");
            var dances = from d in s_dms.Context.Dances select d;
            Assert.AreEqual(s_dances.Count(), dances.Count(), "Count of Dances");
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

            IList<string> dances = s_dms.SerializeDances(false);
            Assert.IsTrue(ListEquivalent(s_dances, dances));

            IList<string> tags = s_dms.SerializeTags(false);
            Assert.IsTrue(ListEquivalent(s_tags, tags));

            // TODONEXT: To get this to work, we have to add in roles to the Mock Context.
            //IList<string> users = s_dms.SerializeUsers(true);
            //Assert.IsTrue(ListEquivalent(s_users, users));
        }

        [TestMethod]
        public void FilterTest()
        {
            var filter = new SongFilter();

            filter.SortOrder = "Tempo";
            filter.Dances = "SWG";
            filter.Purchase = "X";

            var songs = s_dms.BuildSongList(filter,true);

            decimal tempo = 0;
            int count = 0;
            foreach (var song in songs)
            {
                if (song.Tempo.HasValue)
                {
                    Assert.IsTrue(tempo <= song.Tempo);
                    tempo = song.Tempo.Value;
                }

                Assert.IsTrue(song.Purchase.Contains('X'));

                count += 1;
            }

            Trace.WriteLine(string.Format("Filtered Count = {0}", count));
            Assert.AreEqual(89, count);
        }

        [TestMethod]
        public void TopTest()
        {
            var filter = new SongFilter();

            filter.SortOrder = "Dances_10";
            filter.Dances = "SWG";

            var songs = s_dms.BuildSongList(filter, true);

            int weight = int.MaxValue;
            int count = 0;
            foreach (var song in songs)
            {
                DanceRating dr = song.DanceRatings.FirstOrDefault(r => r.DanceId == "SWG");
                Assert.IsTrue(dr.Weight <= weight);

                count += 1;
            }

            Trace.WriteLine(string.Format("Filtered Count = {0}", count));
            Assert.AreEqual(10, count);
        }


        [TestMethod]
        public void SearchTest()
        {
            var filter = new SongFilter();

            filter.SortOrder = "Title";
            filter.SearchString = "The";

            var songs = s_dms.BuildSongList(filter, true);

            string title = string.Empty;
            int count = 0;
            foreach (var song in songs)
            {
                string t = song.Title.ToLower();
                Assert.IsTrue(t.Contains("the") || song.Artist.ToLower().Contains("the") || song.Album.ToLower().Contains("the") );
                Assert.IsTrue(string.Compare(t,title) >=0);
                title = t;

                count += 1;
            }

            Trace.WriteLine(string.Format("Filtered Count = {0}", count));
            Assert.AreEqual(109, count);
        }

        [TestMethod]
        public void PrettyLinkTest()
        {
            string initial = "*East Coast Swing* is a standardized dance in [American Rhythm] style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes [Lindy Hop],  [Carolina Shag], [Balboa], [West Coast Swing], and [Jive].  \r\n\r\nThis dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.\r\n\r\nThe *East Coast Swing* is generally danced as the first dance of [American Rhythm] competitions.";
            string expected = @"*East Coast Swing* is a standardized dance in <a href='http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Rhythm'>American Rhythm</a> style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes <a href='/Dances/Lindy Hop'>Lindy Hop</a>,  <a href='/Dances/Carolina Shag'>Carolina Shag</a>, <a href='/Dances/Balboa'>Balboa</a>, <a href='/Dances/West Coast Swing'>West Coast Swing</a>, and <a href='/Dances/Jive'>Jive</a>.  

This dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.

The *East Coast Swing* is generally danced as the first dance of <a href='http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Rhythm'>American Rhythm</a> competitions.";
            string pretty = Dance.SmartLinks(initial);

            Trace.WriteLine(pretty);
            Assert.AreEqual(expected, pretty);
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
        static List<string> s_dances;
        static List<string> s_tags;
        static List<string> s_songs;

        static DanceMusicService s_dms = new DanceMusicService(new MockContext(false));
    }
}
