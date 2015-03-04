using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class FunctionalTests
    {
        private static readonly DanceMusicTester Service = new DanceMusicTester();

        [TestMethod]
        public void LoadDatabase()
        {
            var users = from u in Service.Dms.Context.Users select u;
            Assert.AreEqual(Service.Users.Count() - 1, users.Count(),"Count of Users");
            var dances = from d in Service.Dms.Context.Dances select d;
            Assert.AreEqual(Service.Dances.Count(), dances.Count(), "Count of Dances");
            var tts = from tt in Service.Dms.Context.TagTypes select tt;
            Assert.AreEqual(Service.Tags.Count(), tts.Count(), "Count of Tag Types");
            var songs = from s in Service.Dms.Context.Songs where s.TitleHash != 0 select s;
            Assert.AreEqual(Service.Songs.Count(), songs.Count(),"Count of Songs");
        }

        [TestMethod]
        public void SaveDatabase()
        {
            Assert.IsNotNull(Service);
            //var songs = 
            Service.Dms.SerializeSongs(false);
            //foreach (string s in songs)
            //{
            //    Trace.WriteLine(s);
            //}
            //Assert.IsTrue(ListEquivalent(s_songs, songs));

            var dances = Service.Dms.SerializeDances(false);
            Assert.IsTrue(ListEquivalent(Service.Dances, dances));

            var tags = Service.Dms.SerializeTags(false);
            Assert.IsTrue(ListEquivalent(Service.Tags, tags));

            // TODO: To get this to work, we have to add in roles to the Mock Context.
            //IList<string> users = s_dms.SerializeUsers(true);
            //Assert.IsTrue(ListEquivalent(s_users, users));
        }

        [TestMethod]
        public void FilterTest()
        {
            var filter = new SongFilter {SortOrder = "Tempo", Dances = "SWG", Purchase = "X"};

            var songs = Service.Dms.BuildSongList(filter);

            decimal tempo = 0;
            var count = 0;
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
            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public void TopTest()
        {
            var filter = new SongFilter {SortOrder = "Dances_10", Dances = "SWG"};


            var songs = Service.Dms.BuildSongList(filter);

            var weight = int.MaxValue;
            var count = 0;
            foreach (var song in songs)
            {
                var dr = song.DanceRatings.FirstOrDefault(r => r.DanceId == "SWG");
                Assert.IsNotNull(dr);
                Assert.IsTrue(dr.Weight <= weight);
                weight = dr.Weight;

                count += 1;
            }

            Trace.WriteLine(string.Format("Filtered Count = {0}", count));
            Assert.AreEqual(10, count);
        }


        [TestMethod]
        public void SearchTest()
        {
            var filter = new SongFilter {SortOrder = "Title", SearchString = "The"};

            var songs = Service.Dms.BuildSongList(filter);

            var title = string.Empty;
            var count = 0;
            foreach (var song in songs)
            {
                string t = song.Title.ToLower();
                Assert.IsTrue(t.Contains("the") || song.Artist.ToLower().Contains("the") || song.Album.ToLower().Contains("the") );
                Assert.IsTrue(String.CompareOrdinal(t, title) >=0);
                title = t;

                count += 1;
            }

            Trace.WriteLine(string.Format("Filtered Count = {0}", count));
            Assert.AreEqual(100, count);
        }

        [TestMethod]
        public void PrettyLinkTest()
        {
            const string initial = "*East Coast Swing* is a standardized dance in [American Rhythm] style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes [Lindy Hop],  [Carolina Shag], [Balboa], [West Coast Swing], and [Jive].  \r\n\r\nThis dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.\r\n\r\nThe *East Coast Swing* is generally danced as the first dance of [American Rhythm] competitions.";
            const string expected = @"*East Coast Swing* is a standardized dance in <a href='http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Rhythm'>American Rhythm</a> style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes <a href='/dances/lindy-hop'>Lindy Hop</a>,  <a href='/dances/carolina-shag'>Carolina Shag</a>, <a href='/dances/balboa'>Balboa</a>, <a href='/dances/west-coast-swing'>West Coast Swing</a>, and <a href='/dances/jive'>Jive</a>.  

This dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.

The *East Coast Swing* is generally danced as the first dance of <a href='http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Rhythm'>American Rhythm</a> competitions.";
            var pretty = Dance.SmartLinks(initial);

            Trace.WriteLine(pretty);
            //for (int i = 0; i < expected.Length && i < pretty.Length; i++)
            //{
            //    if (expected[i] != pretty[i])
            //    {
            //        Trace.WriteLine(string.Format("{0}: '{1}' '{2}'",i,expected[i],pretty[i]));
            //    }
            //}
            Assert.AreEqual(expected, pretty);
        }

        [TestMethod]
        public void TagSuggestionTest()
        {
            const string twoStep = "Night Club Two Step:Dance:187";
            const string childrens = "Children's Music:Music:1";
            const string country = "Country:Music:83";
            const string waltz = "Waltz:Dance:1";
            const string foxtrot = "Foxtrot:Dance:1";
            const string vocal = "Vocal Pop:Music:4";

            var user = Service.Dms.FindUser("batch");
            var userid = new Guid(user.Id);

            ValidateTagSummary(Service.Dms.GetTagSuggestions(), 38, twoStep, childrens, "All Tags");
            ValidateTagSummary(Service.Dms.GetTagSuggestions(userid),34,country, waltz, "Batch Tags");
            ValidateTagSummary(Service.Dms.GetTagSuggestions(userid, null, null, int.MaxValue, true), 32, country, waltz,
                "Batch Normalized Tags");
            ValidateTagSummary(Service.Dms.GetTagSuggestions(userid, 'S', "Music"), 31, country, childrens, "Batch Genre Tags");
            ValidateTagSummary(Service.Dms.GetTagSuggestions(userid, 'S', "Dance"), 3, foxtrot, waltz, "Batch Dance Tags");
            ValidateTagSummary(Service.Dms.GetTagSuggestions(userid, 'S', "Music", 10, true), 10,
                country,vocal,"Top Batch Genere Tags");
        }

        private static void ValidateTagSummary(IEnumerable<TagCount> tags, int expectedCount, string first, string last, string name)
        {
            var list = tags.ToList();
            Trace.WriteLine("All tags=" + list.Count);
            Assert.AreEqual(expectedCount, list.Count, name + " length");

            Trace.WriteLine("First:" + list[0]);
            Assert.AreEqual(first,list[0].Serialize(),name + " first");
            Trace.WriteLine("Last:" + list[list.Count-1]);
            Assert.AreEqual(last, list[list.Count - 1].Serialize(), name + " last");
        }

        static bool ListEquivalent(IEnumerable<string> expected, IList<string> actual)
        {
            var expectedExtra = new List<string>();

            foreach (var e in expected)
            {
                var i = actual.IndexOf(e);
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
                foreach (var s in expectedExtra)
                {
                    Trace.WriteLine(s);
                }
            }
            if (actual.Count <= 0) return actual.Count == 0 && expectedExtra.Count == 0;

            Trace.WriteLine("Actual Extra:\r\n");
            foreach (var s in actual)
            {
                Trace.WriteLine(s);
            }

            return actual.Count == 0 && expectedExtra.Count == 0;
        }
    }
}
