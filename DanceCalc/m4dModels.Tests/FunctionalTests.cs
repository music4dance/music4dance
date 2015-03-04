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
        private static readonly DanceMusicTester s_service = new DanceMusicTester();

        [TestMethod]
        public void LoadDatabase()
        {
            var users = from u in s_service.Dms.Context.Users select u;
            Assert.AreEqual(s_service.Users.Count() - 1, users.Count(),"Count of Users");
            var dances = from d in s_service.Dms.Context.Dances select d;
            Assert.AreEqual(s_service.Dances.Count(), dances.Count(), "Count of Dances");
            var tts = from tt in s_service.Dms.Context.TagTypes select tt;
            Assert.AreEqual(s_service.Tags.Count(), tts.Count(), "Count of Tag Types");
            var songs = from s in s_service.Dms.Context.Songs where s.TitleHash != 0 select s;
            Assert.AreEqual(s_service.Songs.Count(), s_service.Dms.Songs.Count(),"Count of Songs");
        }

        [TestMethod]
        public void SaveDatabase()
        {
            IList<string> songs = s_service.Dms.SerializeSongs(false,true);
            //foreach (string s in songs)
            //{
            //    Trace.WriteLine(s);
            //}
            //Assert.IsTrue(ListEquivalent(s_songs, songs));

            IList<string> dances = s_service.Dms.SerializeDances(false);
            Assert.IsTrue(ListEquivalent(s_service.Dances, dances));

            IList<string> tags = s_service.Dms.SerializeTags(false);
            Assert.IsTrue(ListEquivalent(s_service.Tags, tags));

            // TODO: To get this to work, we have to add in roles to the Mock Context.
            //IList<string> users = s_dms.SerializeUsers(true);
            //Assert.IsTrue(ListEquivalent(s_users, users));
        }

        [TestMethod]
        public void FilterTest()
        {
            var filter = new SongFilter {SortOrder = "Tempo", Dances = "SWG", Purchase = "X"};

            var songs = s_service.Dms.BuildSongList(filter);

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
            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public void TopTest()
        {
            var filter = new SongFilter {SortOrder = "Dances_10", Dances = "SWG"};


            var songs = s_service.Dms.BuildSongList(filter);

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
            var filter = new SongFilter {SortOrder = "Title", SearchString = "The"};

            var songs = s_service.Dms.BuildSongList(filter);

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
            Assert.AreEqual(100, count);
        }

        [TestMethod]
        public void PrettyLinkTest()
        {
            string initial = "*East Coast Swing* is a standardized dance in [American Rhythm] style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes [Lindy Hop],  [Carolina Shag], [Balboa], [West Coast Swing], and [Jive].  \r\n\r\nThis dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.\r\n\r\nThe *East Coast Swing* is generally danced as the first dance of [American Rhythm] competitions.";
            string expected = @"*East Coast Swing* is a standardized dance in <a href='http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Rhythm'>American Rhythm</a> style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes <a href='/dances/lindy-hop'>Lindy Hop</a>,  <a href='/dances/carolina-shag'>Carolina Shag</a>, <a href='/dances/balboa'>Balboa</a>, <a href='/dances/west-coast-swing'>West Coast Swing</a>, and <a href='/dances/jive'>Jive</a>.  

This dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.

The *East Coast Swing* is generally danced as the first dance of <a href='http://en.wikipedia.org/wiki/List_of_DanceSport_dances#Rhythm'>American Rhythm</a> competitions.";
            string pretty = Dance.SmartLinks(initial);

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

            var user = s_service.Dms.FindUser("batch");
            var userid = new Guid(user.Id);

            ValidateTagSummary(s_service.Dms.GetTagSuggestions(), 38, twoStep, childrens, "All Tags");
            ValidateTagSummary(s_service.Dms.GetTagSuggestions(userid),34,country, waltz, "Batch Tags");
            ValidateTagSummary(s_service.Dms.GetTagSuggestions(userid, null, null, int.MaxValue, true), 32, country, waltz,
                "Batch Normalized Tags");
            ValidateTagSummary(s_service.Dms.GetTagSuggestions(userid, 'S', "Music"), 31, country, childrens, "Batch Genre Tags");
            ValidateTagSummary(s_service.Dms.GetTagSuggestions(userid, 'S', "Dance"), 3, foxtrot, waltz, "Batch Dance Tags");
            ValidateTagSummary(s_service.Dms.GetTagSuggestions(userid, 'S', "Music", 10, true), 10,
                country,vocal,"Top Batch Genere Tags");
        }

        private void ValidateTagSummary(IEnumerable<TagCount> tags, int expectedCount, string first, string last, string name)
        {
            var list = tags.ToList();
            Trace.WriteLine("All tags=" + list.Count);
            Assert.AreEqual(expectedCount, list.Count, name + " length");

            Trace.WriteLine("First:" + list[0]);
            Assert.AreEqual(first,list[0].Serialize(),name + " first");
            Trace.WriteLine("Last:" + list[list.Count-1]);
            Assert.AreEqual(last, list[list.Count - 1].Serialize(), name + " last");
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
    }
}
