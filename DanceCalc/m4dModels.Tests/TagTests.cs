using System;
using System.Linq;
using System.Text;
using m4dModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagTests
    {
        static TagTests()
        {
            s_service = new DanceMusicService(new MockContext());
            s_service.SeedDances();

            s_service.FindOrCreateTagType("Swing", "Music");
            s_service.FindOrCreateTagType("Swing", "Dance");
            s_service.FindOrCreateTagType("Salsa", "Music");
            s_service.FindOrCreateTagType("Salsa", "Dance");
            s_service.FindOrCreateTagType("Pop", "Music");
            s_service.FindOrCreateTagType("Foxtrot", "Dance");
        }
        #region TagType
        [TestMethod]
        public void TestCompressTags()
        {
            string t = s_service.CompressTags("Swing|Salsa|Pop|Blues","Music");
            Assert.AreEqual("Blues|Pop|Salsa:Music|Swing:Music", t);

            t = s_service.CompressTags("Salsa|Blues|Foxtrot", "Dance");
            Assert.AreEqual("Blues:Dance|Foxtrot|Salsa:Dance", t);

            t = s_service.CompressTags("Joe|Bro", "Other");
            Assert.AreEqual("Bro|Joe", t);
        }

        [TestMethod]
        public void TestNormalizeTags()
        {
            string t = s_service.NormalizeTags("Swing:Dance|Swing|Salsa|Pop|Blues", "Music");
            Assert.AreEqual("Blues:Music|Pop:Music|Salsa:Music|Swing:Dance|Swing:Music", t);

            t = s_service.NormalizeTags("Salsa|Blues|Foxtrot|Blues:Music", "Dance");
            Assert.AreEqual("Blues:Dance|Blues:Music|Foxtrot:Dance|Salsa:Dance", t);
        }

        #endregion

        #region NewTests
        [TestMethod]
        public void TagTestSummary()
        {
            TagSummary t1 = new TagSummary(_simpleList);
            Assert.AreEqual(4, t1.Tags.Count);
            Assert.AreEqual(1, t1.Tags[0].Count);

            string s1 = t1.ToString();
            Assert.AreEqual(_simpleExpanded, s1);

            TagSummary t2 = new TagSummary(_complexSummary);
            Assert.AreEqual(4, t2.Tags.Count);
            Assert.AreEqual(67, t2.TagCount("Rumba"));
            Assert.AreEqual(23, t2.TagCount("Blues"));

            string s2 = t2.ToString();
            Assert.AreEqual(_complexSummary, s2);
        }

        [TestMethod]
        public void TagTestList()
        {
            TagList l = new TagList(_simpleList);
            Assert.AreEqual(4,l.Tags.Count);
            string s = l.ToString();
            Assert.AreEqual(_simpleList, s);
        }

        [TestMethod]
        public void TagTestSubtract()
        {
            TagList l = new TagList(_simpleSummary);
            TagList l2 = new TagList(_simpleList2);

            TagList lsub = l.Subtract(l2);
            Assert.AreEqual(_simpleSub, lsub.ToString());
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
            SongTagTestChange(s_service);
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

            TagType tt = dms.TagTypes.Find(name);
            Assert.AreNotEqual(null, tt);
            Assert.AreEqual(count, tt.Count);
        }
        void SongTagTestChange(DanceMusicService dms)
        {
            // Create a song
            Song song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", dms);

            // Use batch to add a couple of tags (via change)
            ApplicationUser user = dms == null ? new ApplicationUser("batch") : dms.FindUser("batch");
            song.CreateEditProperties(user,SongBase.EditCommand,dms);
            song.ChangeTags("Rumba:Dance|Bolero:Dance|Latin:Dance|Blues:Dance", user, dms, song);
            Assert.AreEqual("Blues:Dance:1|Bolero:Dance:1|Latin:Dance:1|Rumba:Dance:1", song.TagSummary.ToString());
            VerifyTagCount("Blues:Dance", 1, dms);

            // Use dwgray to add a couple of more tags (via change)
            ApplicationUser user2 = dms == null ? new ApplicationUser("dwgray") : dms.FindUser("dwgray");
            song.CreateEditProperties(user2, SongBase.EditCommand, dms);
            song.ChangeTags("Rumba:Dance|Bolero:Dance|Cha Cha:Dance", user2, dms, song);
            Assert.AreEqual("Blues:Dance:1|Bolero:Dance:2|Cha Cha:Dance:1|Latin:Dance:1|Rumba:Dance:2", song.TagSummary.ToString());
            VerifyTagCount("Bolero:Dance", 2, dms);

            // Use batch to remove a couple of tags (via change)
            song.CreateEditProperties(user, SongBase.EditCommand, dms);
            song.ChangeTags("Rumba:Dance|Bolero:Dance", user, dms, song);
            TagList ut = song.UserTags(user, dms);
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
            string result = ReplaceTime(song.Serialize(new string[] { SongBase.NoSongId }));
            Trace.WriteLine(result);
            string expected = @"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+=Blues:Dance|Bolero:Dance|Latin:Dance|Rumba:Dance	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+=Bolero:Dance|Cha Cha:Dance|Rumba:Dance	User=batchTime=00/00/0000 0:00:00 PM	Tag-=Blues:Dance|Latin:Dance	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+=Blues:Dance	Tag-=Rumba:Dance";
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void SongDanceTagTestChange()
        {
            // Create a song
            Song song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", s_service);

            // Use batch to add a couple of dance ratings and tags
            ApplicationUser user = s_service.FindUser("batch");
            song.CreateEditProperties(user, SongBase.EditCommand, s_service);
            DanceRating dr1 = new DanceRating { DanceId = "BOL", Weight = 5 };
            DanceRating dr2 = new DanceRating { DanceId = "RMB", Weight = 7 };
            song.AddDanceRating(dr1);
            song.AddDanceRating(dr2);

            dr1 = song.FindRating("BOL");
            Assert.AreNotEqual(null, dr1);
            dr2 = song.FindRating("RMB");
            Assert.AreNotEqual(null, dr2);

            song.ChangeDanceTags("BOL","Strict Tempo|Traditional", user, s_service);
            song.ChangeDanceTags("RMB","Non-traditional|Slow", user, s_service);

            Assert.AreEqual("Strict Tempo:1|Traditional:1",dr1.TagSummary.ToString());
            s_service.SaveChanges();

            string result = ReplaceTime(song.Serialize(new string[] { SongBase.NoSongId }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow", result);

            // Now use dwgray to modify one of them
            ApplicationUser user2 = s_service.FindUser("dwgray");
            song.CreateEditProperties(user2, SongBase.EditCommand, s_service);
            song.ChangeDanceTags("BOL", "Traditional", user2, s_service);
            song.ChangeDanceTags("RMB", "Slow|International", user2, s_service);
            s_service.SaveChanges();

            result = ReplaceTime(song.Serialize(new string[] { SongBase.NoSongId }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow", result);

            // Finally use batch to remove a couple of tags
            song.CreateEditProperties(user, SongBase.EditCommand, s_service);
            song.ChangeDanceTags("BOL", null, user, s_service);
            Assert.AreEqual("Traditional:1", dr1.TagSummary.ToString());
            s_service.SaveChanges();

            result = ReplaceTime(song.Serialize(new string[] { SongBase.NoSongId }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batchTime=00/00/0000 0:00:00 PM	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgrayTime=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow	User=batchTime=00/00/0000 0:00:00 PM	Tag-:BOL=Strict Tempo|Traditional", result);
        }

        [TestMethod]
        public void TestTagRing()
        {

        }

        static string ReplaceTime(string s)
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
        static DanceMusicService s_service;


        static string _simpleSummary = "Rumba|Bolero|Latin|Blues";
        static string _simpleList = "Blues|Bolero|Latin|Rumba";
        static string _simpleList2 = "Rumba|Bolero|Cha Cha";
        static string _simpleSub = "Blues|Latin";
        static string _simpleExpanded = "Blues:1|Bolero:1|Latin:1|Rumba:1";
        static string _complexSummary = "Blues:23|Bolero:11|Latin:19|Rumba:67";
        #endregion
    }
}
