using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagTests
    {
        private static async Task<DanceMusicService> GetService()
        {
            var service = await DanceMusicTester.CreateServiceWithUsers("TagTests");
            service.SeedDances();
            return service;
        }

        #region TagAccumulator

        [TestMethod]
        public void TagAccTest()
        {
            var ts1 = new TagSummary(
                "Blues:Music:3|Pop:Music:5|Salsa:Music:7|Swing:Dance:11|Swing:Music:1");
            var ts2 = new TagSummary("Pop:Music:3|Salsa:Music:5|Swing:Music:7|Waltz:Dance:3");

            var ta = new TagAccumulator(ts1.Summary);
            var ts3 = ta.TagSummary();
            Assert.AreEqual(ts1.Summary, ts3.Summary);

            ta.AddTags(ts2.Summary);
            Trace.WriteLine(ta);
            Assert.AreEqual(
                "Blues:Music:3|Pop:Music:8|Salsa:Music:12|Swing:Dance:11|Swing:Music:8|Waltz:Dance:3",
                ta.ToString());

            var ts4 = ta.TagSummary();
            Assert.IsTrue(ts4.HasTag("Blues:Music"));
            Assert.IsTrue(ts4.HasTag("Swing:Dance"));
            Assert.IsTrue(ts4.HasTag("Waltz:Dance"));
            Assert.IsFalse(ts4.HasTag("Tango:Dance"));
        }

        #endregion

        #region TagGroup

        [TestMethod]
        public async Task TestNormalizeTags()
        {
            var service = await GetService();
            var t = service.NormalizeTags("Swing:Dance|Swing|Salsa|Pop|Blues", "Music");
            Assert.AreEqual("Blues:Music|Pop:Music|Salsa:Music|Swing:Dance|Swing:Music", t);

            t = service.NormalizeTags("Salsa|Blues|Foxtrot|Blues:Music", "Dance");
            Assert.AreEqual("Blues:Dance|Blues:Music|Foxtrot:Dance|Salsa:Dance", t);
        }

        [TestMethod]
        public void ReplaceInvalid()
        {
            var result = TagList.Clean(@"Té,st :-12 (ñot).");
            Assert.AreEqual(@"Tést 12 (ñot)", result);
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
            Assert.AreEqual(4, l.Tags.Count);
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
            Assert.AreEqual(2, d.Tags.Count);
        }

        [TestMethod]
        public void TagTestStrip()
        {
            var l = new TagList(QualifiedList);

            var s = new TagList(l.Strip());

            var r = s.ToString();
            Assert.AreEqual("Bolero|Latin|Nontraditional|Pop|Rumba", r);
        }

        [TestMethod]
        public void TagTestExtract()
        {
            var l = new TagList(QualifiedList);

            var add = l.ExtractAdd().ToString();
            Assert.AreEqual("Bolero:Dance|Latin:Music|Nontraditional:Tempo", add);

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
        public void TagVerify()
        {
            var song = new Song();
            for (var index = 0; index < VerifiesInit.Length; index++)
            {
                var init = VerifiesInit[index];
                var res = song.VerifyTags(init, false);
                if (index < 3)
                {
                    Assert.IsNotNull(res);
                }
                else
                {
                    Assert.IsNull(res);
                }
            }
        }


        [TestMethod]
        public void TagVerifyFix()
        {
            var song = new Song();
            for (var index = 0; index < VerifiesInit.Length; index++)
            {
                var init = VerifiesInit[index];
                var res = song.VerifyTags(init).Summary;
                Assert.AreEqual(VerifiesResult[index], res);
            }
        }

        //[TestMethod]
        //public void LikeLoad()
        //{
        //    var service = DanceMusicTester.CreateServiceWithUsers("LikeLoad");

        //    var time = DateTime.Now;
        //    var s = new Song { Created = time, Modified = time };

        //    var o = @"SongId={1da1bacb-80d6-4bce-9c1f-91a363bfa2a2}	.Create=	User=JuliaS	Time=6/5/2014 9:28:47 PM	Title=I Found a Boy	Artist=ADELE	DanceRating=SWZ+2	User=batch	Time=6/7/2014 10:01:47 PM	Artist=Adele	Length=217	Album:00=21	Track:00=12	Purchase:00:IS=420075192	Purchase:00:IA=420075073	PromoteAlbum:00=	User=batch	Time=10/9/2014 11:00:32 AM	DanceRating=WLZ+2	User=JuliaS	Time=11/20/2014 11:30:23 AM	Tag+=Slow Waltz:Dance	User=batch	Time=11/20/2014 11:30:23 AM	Tag+=Pop:Music	.Edit=	User=batch-i	Time=12/10/2014 6:39:48 PM	Tag+=Pop:Music	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/17/2016 16:34:09	Sample=http://a1243.phobos.apple.com/us/r1000/074/Music/9a/98/81/mzm.vhwdgmvx.aac.p.m4a	.Edit=	User=dwgray	Time=04/29/2016 00:24:24	DanceRating=SWZ+2	Tag+=Slow Waltz:Dance	.Edit=	User=dwgray	Time=04/29/2016 00:25:22	Tempo=170.0	Sample=	DanceRating=VWZ+2	DanceRating=WLZ+2	DanceRating=SWZ-3	Tag+=!Slow Waltz:Dance|Viennese Waltz:Dance|Waltz:Dance	Tag-=Slow Waltz:Dance	DanceRating=WLZ+2	DanceRating=VWZ+1	.Edit=	User=dwgray	Time=04/29/2016 00:25:30	Like=true";
        //    s.Load(o, service.DanceStats);

        //    var song = service.FindSong(new Guid("1da1bacb-80d6-4bce-9c1f-91a363bfa2a2"));
        //    Assert.IsNotNull(song);

        //    var mr = song.ModifiedBy.FirstOrDefault(r => r.UserName == "dwgray");
        //    Assert.IsNotNull(mr);
        //    Assert.IsNotNull(mr.Like);
        //    Assert.IsTrue(mr.Like.Value);
        //}

        private static readonly string[] VerifiesInit =
        [
            "",
            "Blues:dance|Pop:music",
            "4/4:tempo|fast:tempo",
            "Crazy|Christmas:Christmas"
        ];

        private static readonly string[] VerifiesResult =
        [
            "",
            "Blues:Dance|Pop:Music",
            "4/4:Tempo|fast:Tempo",
            "Christmas:Other|Crazy:Other"
        ];

        private const string SimpleSummary = "Rumba|Bolero|Latin|Blues";
        private const string SimpleList = "Blues|Bolero|Latin|Rumba";
        private const string SimpleList2 = "Rumba|Bolero|Cha Cha";
        private const string SimpleSub = "Blues|Latin";
        private const string SimpleAdd = "Blues|Bolero|Cha Cha|Latin|Rumba";
        private const string SimpleExpanded = "Blues:1|Bolero:1|Latin:1|Rumba:1";
        private const string ComplexSummary = "Blues:23|Bolero:11|Latin:19|Rumba:67";

        private const string QualifiedList =
            "+Bolero:Dance|+Latin:Music|+Nontraditional:Tempo|-Rumba:Dance|-Pop:Music";

        #endregion
    }
}
