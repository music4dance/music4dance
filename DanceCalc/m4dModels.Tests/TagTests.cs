using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagTests
    {
        static TagTests()
        {
            SService = MockContext.CreateService(true);
            SService.SeedDances();

            SService.FindOrCreateTagType("Swing", "Music");
            SService.FindOrCreateTagType("Swing", "Dance");
            SService.FindOrCreateTagType("Salsa", "Music");
            SService.FindOrCreateTagType("Salsa", "Dance");
            SService.FindOrCreateTagType("Pop", "Music");
            var tt = SService.FindOrCreateTagType("Foxtrot", "Dance");
            var tt1 = SService.FindOrCreateTagType("fox-trot", "Dance","FoxTrot");
            var tt2 = SService.FindOrCreateTagType("Fox Trot", "Dance", "FoxTrot");

            tt1.Primary = tt;
            tt2.Primary = tt;
        }
        #region TagType
        [TestMethod]
        public void TestCompressTags()
        {
            var t = SService.CompressTags("Swing|Salsa|Pop|Blues","Music");
            Assert.AreEqual("Blues|Pop|Salsa:Music|Swing:Music", t);

            t = SService.CompressTags("Salsa|Blues|Foxtrot", "Dance");
            Assert.AreEqual("Blues:Dance|Foxtrot|Salsa:Dance", t);

            t = SService.CompressTags("Joe|Bro", "Other");
            Assert.AreEqual("Bro|Joe", t);
        }

        [TestMethod]
        public void TestNormalizeTags()
        {
            var t = SService.NormalizeTags("Swing:Dance|Swing|Salsa|Pop|Blues", "Music");
            Assert.AreEqual("Blues:Music|Pop:Music|Salsa:Music|Swing:Dance|Swing:Music", t);

            t = SService.NormalizeTags("Salsa|Blues|Foxtrot|Blues:Music", "Dance");
            Assert.AreEqual("Blues:Dance|Blues:Music|Foxtrot:Dance|Salsa:Dance", t);
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


        // TODO: Not sure we can test this disconnected from the EF stuff...
        //[TestMethod]
        //public void DanceTagTestChange()
        //{
        //    TagType ecsLong = s_service.FindOrCreateTagType("East Coast Swing", "Dance");
        //    TagType ecsShort = s_service.FindOrCreateTagType("ECS", "Dance");
        //    TagType ecsMed = s_service.FindOrCreateTagType("EC Swing", "Dance");

        //    ecsShort.Primary = ecsLong;
        //    ecsMed.Primary = ecsLong;
        //}

        //[TestMethod]
        //public void UserTagTestChange()
        //{

        //}

        [TestMethod]
        public void SongTagTestChangeWithService()
        {
            SongTagTestChange(SService);
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
            const string expected = @"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+=Blues:Dance|Bolero:Dance|Latin:Dance|Rumba:Dance	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+=Bolero:Dance|Cha Cha:Dance|Rumba:Dance	User=batchTime=00/00/0000 0:00:00 PM	Tag-=Blues:Dance|Latin:Dance	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+=Blues:Dance	Tag-=Rumba:Dance";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SongTagUpdateSummary()
        {
            // Create a song
            var song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", SService);

            // Use batch to add a couple of tags (via change)
            var user = SService == null ? new ApplicationUser("batch") : SService.FindUser("batch");
            var user2 = SService == null ? new ApplicationUser("dwgray") : SService.FindUser("dwgray");

            song.CreateEditProperties(user, SongBase.EditCommand, SService);
            song.ChangeTags("fox-trot:Dance|Swing:Dance", user, SService, song);

            song.CreateEditProperties(user2, SongBase.EditCommand, SService);
            song.ChangeTags("Fox Trot:Dance|Swing:Dance", user2, SService, song);

            Trace.WriteLine(song.TagSummary.ToString());
            const string expected = @"Foxtrot:Dance:2|Swing:Dance:2";
            Assert.AreEqual(expected,song.TagSummary.ToString());

            // ReSharper disable once PossibleNullReferenceException
            var cft = SService.TagTypes.Find("Foxtrot:Dance").Count;
            var cswing = SService.TagTypes.Find("Swing:Dance").Count;

            song.UpdateTagSummary(SService);
            Assert.AreEqual(expected, song.TagSummary.ToString());

            Assert.AreEqual(cft, SService.TagTypes.Find("Foxtrot:Dance").Count);
            Assert.AreEqual(cswing, SService.TagTypes.Find("Swing:Dance").Count);
        }

        [TestMethod]
        public void SongDanceTagTestChange()
        {
            // Create a song
            var song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", SService);

            // Use batch to add a couple of dance ratings and tags
            var user = SService.FindUser("batch");
            song.CreateEditProperties(user, SongBase.EditCommand, SService);
            var dr1 = new DanceRating { DanceId = "BOL", Weight = 5 };
            var dr2 = new DanceRating { DanceId = "RMB", Weight = 7 };
            song.AddDanceRating(dr1);
            song.AddDanceRating(dr2);

            dr1 = song.FindRating("BOL");
            Assert.AreNotEqual(null, dr1);
            dr2 = song.FindRating("RMB");
            Assert.AreNotEqual(null, dr2);

            song.ChangeDanceTags("BOL","Strict Tempo|Traditional", user, SService);
            song.ChangeDanceTags("RMB","Non-traditional|Slow", user, SService);

            Assert.AreEqual("Strict Tempo:1|Traditional:1",dr1.TagSummary.ToString());
            SService.SaveChanges();

            var result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow", result);

            // Now use dwgray to modify one of them
            var user2 = SService.FindUser("dwgray");
            song.CreateEditProperties(user2, SongBase.EditCommand, SService);
            song.ChangeDanceTags("BOL", "Traditional", user2, SService);
            song.ChangeDanceTags("RMB", "Slow|International", user2, SService);
            SService.SaveChanges();

            result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow", result);

            // Finally use batch to remove a couple of tags
            song.CreateEditProperties(user, SongBase.EditCommand, SService);
            song.ChangeDanceTags("BOL", null, user, SService);
            Assert.AreEqual("Traditional:1", dr1.TagSummary.ToString());
            SService.SaveChanges();

            result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow	User=batchTime=00/00/0000 0:00:00 PM	Tag-:BOL=Strict Tempo|Traditional", result);
        }


        static readonly DanceMusicService SService;


        private const string SimpleSummary = "Rumba|Bolero|Latin|Blues";
        private const string SimpleList = "Blues|Bolero|Latin|Rumba";
        private const string SimpleList2 = "Rumba|Bolero|Cha Cha";
        private const string SimpleSub = "Blues|Latin";
        private const string SimpleExpanded = "Blues:1|Bolero:1|Latin:1|Rumba:1";
        private const string ComplexSummary = "Blues:23|Bolero:11|Latin:19|Rumba:67";

        #endregion
    }
}
