using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagTests
    {
        static TagTests()
        {
            s_sService = MockContext.CreateService(true);
            s_sService.SeedDances();

            s_sService.FindOrCreateTagType("Swing", "Music");
            s_sService.FindOrCreateTagType("Swing", "Dance");
            s_sService.FindOrCreateTagType("Salsa", "Music");
            s_sService.FindOrCreateTagType("Salsa", "Dance");
            s_sService.FindOrCreateTagType("Pop", "Music");
            var tt = s_sService.FindOrCreateTagType("Foxtrot", "Dance");
            var tt1 = s_sService.FindOrCreateTagType("fox-trot", "Dance","FoxTrot");
            var tt2 = s_sService.FindOrCreateTagType("Fox Trot", "Dance", "FoxTrot");

            tt1.Primary = tt;
            tt2.Primary = tt;
        }
        #region TagType
        [TestMethod]
        public void TestCompressTags()
        {
            var t = s_sService.CompressTags("Swing|Salsa|Pop|Blues","Music");
            Assert.AreEqual("Blues|Pop|Salsa:Music|Swing:Music", t);

            t = s_sService.CompressTags("Salsa|Blues|Foxtrot", "Dance");
            Assert.AreEqual("Blues:Dance|Foxtrot|Salsa:Dance", t);

            t = s_sService.CompressTags("Joe|Bro", "Other");
            Assert.AreEqual("Bro|Joe", t);
        }

        [TestMethod]
        public void TestNormalizeTags()
        {
            var t = s_sService.NormalizeTags("Swing:Dance|Swing|Salsa|Pop|Blues", "Music");
            Assert.AreEqual("Blues:Music|Pop:Music|Salsa:Music|Swing:Dance|Swing:Music", t);

            t = s_sService.NormalizeTags("Salsa|Blues|Foxtrot|Blues:Music", "Dance");
            Assert.AreEqual("Blues:Dance|Blues:Music|Foxtrot:Dance|Salsa:Dance", t);
        }

        #endregion

        #region TagAccumulator

        [TestMethod]
        public void TagAccTest()
        {
            var ts1 = new TagSummary("Blues:Music:3|Pop:Music:5|Salsa:Music:7|Swing:Dance:11|Swing:Music:1");
            var ts2 = new TagSummary("Pop:Music:3|Salsa:Music:5|Swing:Music:7|Waltz:Dance:3");

            var ta = new TagAccumulator(ts1.Summary);
            var ts3 = ta.TagSummary();
            Assert.AreEqual(ts1.Summary,ts3.Summary);

            ta.AddTags(ts2.Summary);
            Trace.WriteLine(ta);
            Assert.AreEqual("Blues:Music:3|Pop:Music:8|Salsa:Music:12|Swing:Dance:11|Swing:Music:8|Waltz:Dance:3",ta.ToString());
        }

        #endregion

        #region NewTests
        [TestMethod]
        public void TagTestSummary()
        {
            var t1 = new TagSummary(SimpleList);
            Assert.AreEqual(4, t1.Tags.Count);
            Assert.AreEqual(1, t1.Tags[0].Count);

            var s1 = t1.ToString();
            Assert.AreEqual(SimpleExpanded, s1);

            var t2 = new TagSummary(ComplexSummary);
            Assert.AreEqual(4, t2.Tags.Count);
            Assert.AreEqual(67, t2.TagCount("Rumba"));
            Assert.AreEqual(23, t2.TagCount("Blues"));

            var s2 = t2.ToString();
            Assert.AreEqual(ComplexSummary, s2);
        }

        [TestMethod]
        public void TagTestList()
        {
            var l = new TagList(SimpleList);
            Assert.AreEqual(4,l.Tags.Count);
            var s = l.ToString();
            Assert.AreEqual(SimpleList, s);
        }

        [TestMethod]
        public void TagTestSubtract()
        {
            var l = new TagList(SimpleSummary);
            var l2 = new TagList(SimpleList2);

            var lsub = l.Subtract(l2);
            Assert.AreEqual(SimpleSub, lsub.ToString());
        }

        [TestMethod]
        public void TagTestAdd()
        {
            var l = new TagList(SimpleSummary);
            var l2 = new TagList(SimpleList2);

            var ladd = l.Add(l2);
            Assert.AreEqual(SimpleAdd, ladd.ToString());
        }

        [TestMethod]
        public void TagTestFilter()
        {
            var l = new TagList(QualifiedList);

            var d = l.Filter("Dance");
            Assert.AreEqual(2,d.Tags.Count);
        }

        [TestMethod]
        public void TagTestStrip()
        {
            var l = new TagList(QualifiedList);

            var s = new TagList(l.Strip());

            var r = s.ToString();
            Assert.AreEqual(r, "Bolero|Latin|Nontraditional|Pop|Rumba");
        }

        [TestMethod]
        public void TagTestExtract()
        {
            var l = new TagList(QualifiedList);

            var add = l.ExtractAdd().ToString();
            Assert.AreEqual("Bolero:Dance|Latin:Music|Nontraditional:Tempo",add);

            var rem = l.ExtractRemove().ToString();
            Assert.AreEqual("Pop:Music|Rumba:Dance", rem);
        }

        [TestMethod]
        public void TagTestQualifiedSubtract()
        {
            var l = new TagList(QualifiedList);
            var l2 = new TagList("Bolero:Dance|Nontraditional:Tempo|Rumba:Dance|Pop:Music");

            var lsub = l.Subtract(l2);
            Assert.AreEqual("+Latin:Music", lsub.ToString());

            l2 = new TagList("-Bolero:Dance|+Nontraditional:Tempo|+Rumba:Dance|-Pop:Music");
            lsub = l.Subtract(l2);
            Assert.AreEqual("+Latin:Music", lsub.ToString());
        }

        [TestMethod]
        public void SongTagTestChangeWithService()
        {
            SongTagTestChange(s_sService);
        }

        [TestMethod]
        public void SongTagTestChangeNoService()
        {
            SongTagTestChange(null);
        }

        void VerifyTagCount(string name, int count, DanceMusicService dms)
        {
            if (dms == null)
            {
                return;
            }

            var tt = dms.TagTypes.Find(name);
            Assert.AreNotEqual(null, tt);
            Assert.AreEqual(count, tt.Count);
        }

        void SongTagTestChange(DanceMusicService dms)
        {
            // Create a song
            var song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", dms);

            // Use batch to add a couple of tags (via change)
            var user = dms == null ? new ApplicationUser("batch") : dms.FindUser("batch");
            song.CreateEditProperties(user,SongBase.EditCommand,dms);
            song.ChangeTags("Rumba:Dance|Bolero:Dance|Latin:Dance|Blues:Dance", user, dms, song);
            Assert.AreEqual("Blues:Dance:1|Bolero:Dance:1|Latin:Dance:1|Rumba:Dance:1", song.TagSummary.ToString());
            VerifyTagCount("Blues:Dance", 1, dms);

            // Use dwgray to add a couple of more tags (via change)
            var user2 = dms == null ? new ApplicationUser("dwgray") : dms.FindUser("dwgray");
            song.CreateEditProperties(user2, SongBase.EditCommand, dms);
            song.ChangeTags("Rumba:Dance|Bolero:Dance|Cha Cha:Dance", user2, dms, song);
            Assert.AreEqual("Blues:Dance:1|Bolero:Dance:2|Cha Cha:Dance:1|Latin:Dance:1|Rumba:Dance:2", song.TagSummary.ToString());
            VerifyTagCount("Bolero:Dance", 2, dms);

            // Use batch to remove a couple of tags (via change)
            song.CreateEditProperties(user, SongBase.EditCommand, dms);
            song.ChangeTags("Rumba:Dance|Bolero:Dance", user, dms, song);
            var ut = song.UserTags(user, dms);
            Assert.AreEqual("Bolero:Dance|Rumba:Dance", ut.ToString());
            Assert.AreEqual("Bolero:Dance:2|Cha Cha:Dance:1|Rumba:Dance:2", song.TagSummary.ToString());
            VerifyTagCount("Cha Cha:Dance", 1, dms);

            // Use dwgray to add a couple of tags (via add)
            song.CreateEditProperties(user2, SongBase.EditCommand, dms);
            song.AddTags("Rumba:Dance|Blues:Dance", user2, dms, song);
            Assert.AreEqual("Blues:Dance:1|Bolero:Dance:2|Cha Cha:Dance:1|Rumba:Dance:2", song.TagSummary.ToString());

            // Use dwgray to remove a couple of tags (via remove)
            song.RemoveTags("Rumba:Dance|Latin:Dance", user2, dms, song);
            Assert.AreEqual("Blues:Dance:1|Bolero:Dance:2|Cha Cha:Dance:1|Rumba:Dance:1", song.TagSummary.ToString());
            VerifyTagCount("Rumba:Dance", 1, dms);

            // Check the serialized result of the whole mess
            var result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            const string expected = @"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batch	Time=00/00/0000 0:00:00 PM	Tag+=Blues:Dance|Bolero:Dance|Latin:Dance|Rumba:Dance	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Bolero:Dance|Cha Cha:Dance|Rumba:Dance	User=batch	Time=00/00/0000 0:00:00 PM	Tag-=Blues:Dance|Latin:Dance	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Blues:Dance	Tag-=Rumba:Dance";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SongTagUpdateSummary()
        {
            // Create a song
            var song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", s_sService);

            // Use batch to add a couple of tags (via change)
            var user = s_sService == null ? new ApplicationUser("batch") : s_sService.FindUser("batch");
            var user2 = s_sService == null ? new ApplicationUser("dwgray") : s_sService.FindUser("dwgray");

            song.CreateEditProperties(user, SongBase.EditCommand, s_sService);
            song.ChangeTags("fox-trot:Dance|Swing:Dance", user, s_sService, song);

            song.CreateEditProperties(user2, SongBase.EditCommand, s_sService);
            song.ChangeTags("Fox Trot:Dance|Swing:Dance", user2, s_sService, song);

            Trace.WriteLine(song.TagSummary.ToString());
            const string expected = @"Foxtrot:Dance:2|Swing:Dance:2";
            Assert.AreEqual(expected,song.TagSummary.ToString());

            // ReSharper disable once PossibleNullReferenceException
            var cft = s_sService.TagTypes.Find("Foxtrot:Dance").Count;
            var cswing = s_sService.TagTypes.Find("Swing:Dance").Count;

            song.UpdateTagSummary(s_sService);
            Assert.AreEqual(expected, song.TagSummary.ToString());

            Assert.AreEqual(cft, s_sService.TagTypes.Find("Foxtrot:Dance").Count);
            Assert.AreEqual(cswing, s_sService.TagTypes.Find("Swing:Dance").Count);
        }

        [TestMethod]
        public void SongDanceTagTestChange()
        {
            // Create a song
            var song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", s_sService);

            // Use batch to add a couple of dance ratings and tags
            var user = s_sService.FindUser("batch");
            song.CreateEditProperties(user, SongBase.EditCommand, s_sService);
            var dr1 = new DanceRating { DanceId = "BOL", Weight = 5 };
            var dr2 = new DanceRating { DanceId = "RMB", Weight = 7 };
            song.CreateDanceRatings(new[] {dr1,dr2},s_sService);

            dr1 = song.FindRating("BOL");
            Assert.AreNotEqual(null, dr1);
            dr2 = song.FindRating("RMB");
            Assert.AreNotEqual(null, dr2);

            song.ChangeDanceTags("BOL","Strict Tempo|Traditional", user, s_sService);
            song.ChangeDanceTags("RMB","Non-traditional|Slow", user, s_sService);

            Assert.AreEqual("Strict Tempo:1|Traditional:1",dr1.TagSummary.ToString());
            s_sService.SaveChanges();

            var result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batch	Time=00/00/0000 0:00:00 PM	DanceRating=BOL+5	DanceRating=RMB+7	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow", result);

            // Now use dwgray to modify one of them
            var user2 = s_sService.FindUser("dwgray");
            song.CreateEditProperties(user2, SongBase.EditCommand, s_sService);
            song.ChangeDanceTags("BOL", "Traditional", user2, s_sService);
            song.ChangeDanceTags("RMB", "Slow|International", user2, s_sService);
            s_sService.SaveChanges();

            result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batch	Time=00/00/0000 0:00:00 PM	DanceRating=BOL+5	DanceRating=RMB+7	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow", result);

            // Finally use batch to remove a couple of tags
            song.CreateEditProperties(user, SongBase.EditCommand, s_sService);
            song.ChangeDanceTags("BOL", null, user, s_sService);
            Assert.AreEqual("Traditional:1", dr1.TagSummary.ToString());
            s_sService.SaveChanges();

            result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batch	Time=00/00/0000 0:00:00 PM	DanceRating=BOL+5	DanceRating=RMB+7	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow	User=batch	Time=00/00/0000 0:00:00 PM	Tag-:BOL=Strict Tempo|Traditional", result);
        }


        static readonly DanceMusicService s_sService;


        private const string SimpleSummary = "Rumba|Bolero|Latin|Blues";
        private const string SimpleList = "Blues|Bolero|Latin|Rumba";
        private const string SimpleList2 = "Rumba|Bolero|Cha Cha";
        private const string SimpleSub = "Blues|Latin";
        private const string SimpleAdd = "Blues|Bolero|Cha Cha|Latin|Rumba";
        private const string SimpleExpanded = "Blues:1|Bolero:1|Latin:1|Rumba:1";
        private const string ComplexSummary = "Blues:23|Bolero:11|Latin:19|Rumba:67";

        private const string QualifiedList = "+Bolero:Dance|+Latin:Music|+Nontraditional:Tempo|-Rumba:Dance|-Pop:Music";

        #endregion
    }
}
