using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagTests
    {
        static TagTests()
        {
            Service = MockContext.CreateService(true);
            Service.SeedDances();

            Service.FindOrCreateTagType("Swing", "Music");
            Service.FindOrCreateTagType("Swing", "Dance");
            Service.FindOrCreateTagType("Salsa", "Music");
            Service.FindOrCreateTagType("Salsa", "Dance");
            Service.FindOrCreateTagType("Pop", "Music");
            var tt = Service.FindOrCreateTagType("Foxtrot", "Dance");
            var tt1 = Service.FindOrCreateTagType("fox-trot", "Dance","FoxTrot");
            var tt2 = Service.FindOrCreateTagType("Fox Trot", "Dance", "FoxTrot");

            tt1.Primary = tt;
            tt2.Primary = tt;
        }
        #region TagType
        [TestMethod]
        public void TestCompressTags()
        {
            var t = Service.CompressTags("Swing|Salsa|Pop|Blues","Music");
            Assert.AreEqual("Blues|Pop|Salsa:Music|Swing:Music", t);

            t = Service.CompressTags("Salsa|Blues|Foxtrot", "Dance");
            Assert.AreEqual("Blues:Dance|Foxtrot|Salsa:Dance", t);

            t = Service.CompressTags("Joe|Bro", "Other");
            Assert.AreEqual("Bro|Joe", t);
        }

        [TestMethod]
        public void TestNormalizeTags()
        {
            var t = Service.NormalizeTags("Swing:Dance|Swing|Salsa|Pop|Blues", "Music");
            Assert.AreEqual("Blues:Music|Pop:Music|Salsa:Music|Swing:Dance|Swing:Music", t);

            t = Service.NormalizeTags("Salsa|Blues|Foxtrot|Blues:Music", "Dance");
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
            SongTagTestChange(Service);
        }

        [TestMethod]
        public void SongTagTestChangeNoService()
        {
            SongTagTestChange(null);
        }

        static void VerifyTagCount(string name, int count, DanceMusicService dms)
        {
            if (dms == null)
            {
                return;
            }

            var tt = dms.TagTypes.Find(name);
            Assert.AreNotEqual(null, tt);
            Assert.AreEqual(count, tt.Count);
        }

        static void SongTagTestChange(DanceMusicService dms)
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
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", Service);

            // Use batch to add a couple of tags (via change)
            var user = Service == null ? new ApplicationUser("batch") : Service.FindUser("batch");
            var user2 = Service == null ? new ApplicationUser("dwgray") : Service.FindUser("dwgray");

            song.CreateEditProperties(user, SongBase.EditCommand, Service);
            song.ChangeTags("fox-trot:Dance|Swing:Dance", user, Service, song);

            song.CreateEditProperties(user2, SongBase.EditCommand, Service);
            song.ChangeTags("Fox Trot:Dance|Swing:Dance", user2, Service, song);

            Trace.WriteLine(song.TagSummary.ToString());
            const string expected = @"Foxtrot:Dance:2|Swing:Dance:2";
            Assert.AreEqual(expected,song.TagSummary.ToString());

            // ReSharper disable once PossibleNullReferenceException
            var cft = Service.TagTypes.Find("Foxtrot:Dance").Count;
            var cswing = Service.TagTypes.Find("Swing:Dance").Count;

            song.UpdateTagSummary(Service);
            Assert.AreEqual(expected, song.TagSummary.ToString());

            Assert.AreEqual(cft, Service.TagTypes.Find("Foxtrot:Dance").Count);
            Assert.AreEqual(cswing, Service.TagTypes.Find("Swing:Dance").Count);
        }

        [TestMethod]
        public void SongDanceTagTestChange()
        {
            // Create a song
            var song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0", Service);

            // Use batch to add a couple of dance ratings and tags
            var user = Service.FindUser("batch");
            song.CreateEditProperties(user, SongBase.EditCommand, Service);
            var dr1 = new DanceRating { DanceId = "BOL", Weight = 5 };
            var dr2 = new DanceRating { DanceId = "RMB", Weight = 7 };
            song.CreateDanceRatings(new[] {dr1,dr2},Service);

            dr1 = song.FindRating("BOL");
            Assert.AreNotEqual(null, dr1);
            dr2 = song.FindRating("RMB");
            Assert.AreNotEqual(null, dr2);

            song.ChangeDanceTags("BOL","Strict Tempo|Traditional", user, Service);
            song.ChangeDanceTags("RMB","Non-traditional|Slow", user, Service);

            Assert.AreEqual("Strict Tempo:1|Traditional:1",dr1.TagSummary.ToString());
            Service.SaveChanges();

            var result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batch	Time=00/00/0000 0:00:00 PM	DanceRating=BOL+5	DanceRating=RMB+7	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow", result);

            // Now use dwgray to modify one of them
            var user2 = Service.FindUser("dwgray");
            song.CreateEditProperties(user2, SongBase.EditCommand, Service);
            song.ChangeDanceTags("BOL", "Traditional", user2, Service);
            song.ChangeDanceTags("RMB", "Slow|International", user2, Service);
            Service.SaveChanges();

            result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batch	Time=00/00/0000 0:00:00 PM	DanceRating=BOL+5	DanceRating=RMB+7	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow", result);

            // Finally use batch to remove a couple of tags
            song.CreateEditProperties(user, SongBase.EditCommand, Service);
            song.ChangeDanceTags("BOL", null, user, Service);
            Assert.AreEqual("Traditional:1", dr1.TagSummary.ToString());
            Service.SaveChanges();

            result = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId, SongBase.EditCommand }));
            Trace.WriteLine(result);
            Assert.AreEqual(@"user=batch	Title=Test	Artist=Me	Tempo=30.0	User=batch	Time=00/00/0000 0:00:00 PM	DanceRating=BOL+5	DanceRating=RMB+7	Tag+:BOL=Strict Tempo|Traditional	Tag+:RMB=Non-traditional|Slow	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+:BOL=Traditional	Tag+:RMB=International|Slow	User=batch	Time=00/00/0000 0:00:00 PM	Tag-:BOL=Strict Tempo|Traditional", result);
        }

        [TestMethod]
        public void ReloadTags()
        {
            const string s1 = @"SongId={81b16653-b1fc-4a95-81be-87a323cd2ab7}	.Create=	User=OliviaL	Time=10/3/2014 1:01:14 PM	Title=Ain't No Mountain High Enough	Artist=Marvin Gaye & Tammi Terrell	Tempo=132.0	DanceRating=MWT+5	User=batch	Time=10/3/2014 1:02:30 PM	Length=146	Album:00=The Very Best Of Marvin Gaye	Track:00=10	Purchase:00:XS=music.C53A0C00-0100-11DB-89CA-0019B92A3933	User=batch	Time=10/3/2014 1:06:12 PM	Album:01=Gold: Marvin Gaye	Track:01=10	Purchase:01:IS=41562342	Purchase:01:IA=41562284	OrderAlbums=1,0	User=OliviaL	Time=11/20/2014 11:29:25 AM	Tag+=Motown:Music	.Edit=	User=batch-i	Time=12/10/2014 1:27:45 PM	Length=149	Album:02=The Complete Collection	Track:02=7	Purchase:02:IS=425795975	Purchase:02:IA=425795892	Album:03=A Motown Thanksgiving Celebration - EP	Track:03=2	Purchase:03:IS=339341404	Purchase:03:IA=339341225	Album:04=Gold - More Motown Classics	Track:04=19	Purchase:04:IS=256228360	Purchase:04:IA=256228195	Album:05=Love Songs	Track:05=1	Purchase:05:IS=408826010	Purchase:05:IA=408825939	Album:06=The Master (1961-1984)	Track:06=5	Purchase:06:IS=381679	Purchase:06:IA=381830	Album:07=United	Track:07=1	Purchase:07:IS=940330972	Purchase:07:IA=940330971	Album:08=Marvin Gaye & His Women	Track:08=9	Purchase:08:IS=610617834	Purchase:08:IA=610616164	Album:09=The Complete Motown Singles Vol. 7: 1967	Track:09=21	Purchase:09:IS=256230618	Purchase:09:IA=256230309	Album:10=Marvin Gaye: The Albums 1960s	Track:10=1	Purchase:10:IS=940344982	Purchase:10:IA=940342707	Album:11=Hitsville USA - The Motown Singles Collection, 1959-1971	Track:11=8	Purchase:11:IS=300588785	Purchase:11:IA=300588633	Album:12=Anthology: The Best of Marvin Gaye	Track:12=16	Purchase:12:IS=564221113	Purchase:12:IA=564220679	Album:13=20th Century Masters - The Millennium Collection: The Best of Marvin Gaye & Tammi Terrell	Track:13=2	Purchase:13:IS=523378	Purchase:13:IA=523427	Album:14=Guardians of the Galaxy: Awesome Mix, Vol. 1 (Original Motion Picture Soundtrack)	Track:14=12	Purchase:14:IS=895283671	Purchase:14:IA=895283652	Album:15=Motown the Musical - 100 Originals	Track:15=5	Purchase:15:IS=632315251	Purchase:15:IA=632314754	OrderAlbums=1,0,2,3,4,5,6,7,8,9,10,11,12,13,14,15	Tag+=R&B/Soul:Music|Soundtrack:Music	.Edit=	User=batch-x	Time=12/10/2014 1:27:58 PM	Length=146	Purchase:02:XS=music.BFA4BA06-0100-11DB-89CA-0019B92A3933	Purchase:05:XS=music.D3D8A306-0100-11DB-89CA-0019B92A3933	Album:06=The Master 1961-1984	Track:06=33	Purchase:06:XS=music.854F0A00-0100-11DB-89CA-0019B92A3933	Purchase:07:XS=music.DF7E1A02-0100-11DB-89CA-0019B92A3933	Album:09=The Complete Motown Singles, Vol.7: 1967	Track:09=45	Purchase:09:XS=music.3335A200-0100-11DB-89CA-0019B92A3933	Album:11=Hitsville USA - The Motown Singles Collection 1959-1971	Track:11=63	Purchase:11:XS=music.DFAD6601-0100-11DB-89CA-0019B92A3933	Album:12=Anthology: The Best Of Marvin Gaye	Purchase:12:XS=music.25046607-0100-11DB-89CA-0019B92A3933	Album:13=20th Century Masters - The Millennium Collection: The Best Of Marvin Gaye & Tammi Terrell	Purchase:13:XS=music.7F920A00-0100-11DB-89CA-0019B92A3933	Album:16=Guardians of the Galaxy: Awesome Mix Vol. 1	Track:16=12	Purchase:16:XS=music.89986B08-0100-11DB-89CA-0019B92A3933	Album:17=Marvin Gaye & His Women - 21 Classic Duets	Track:17=9	Purchase:17:XS=music.7E7FA707-0100-11DB-89CA-0019B92A3933	Album:18=Anthology	Track:18=16	Purchase:18:XS=music.45500E00-0100-11DB-89CA-0019B92A3933	Album:19=Motown For Kids	Track:19=14	Purchase:19:XS=music.9972F000-0100-11DB-89CA-0019B92A3933	Album:20=The Complete Duets	Track:20=1	Purchase:20:XS=music.B7100D00-0100-11DB-89CA-0019B92A3933	Album:21=Playlist Plus: Motown 50	Track:21=33	Purchase:21:XS=music.AD66D000-0100-11DB-89CA-0019B92A3933	Album:22=More Motown Classics Gold (Remastered)	Track:22=19	Purchase:22:XS=music.8934A200-0100-11DB-89CA-0019B92A3933	Album:23=Favorites	Track:23=7	Purchase:23:XS=music.D1DCE600-0100-11DB-89CA-0019B92A3933	Album:24=Gold	Track:24=10	Purchase:24:XS=music.77593300-0100-11DB-89CA-0019B92A3933	Album:25=A Motown Thanksgiving Celebration (5-Track Maxi-Single)	Track:25=2	Purchase:25:XS=music.B3751302-0100-11DB-89CA-0019B92A3933	OrderAlbums=1,0,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25	Tag+=R&B / Soul:Music|Soundtracks:Music	.FailedLookup=-:0	.FailedLookup=s:0	.Edit=	User=dwgray	Time=1/12/2015 1:00:28 PM	Publisher:00=Rhino	OwnerHash=1598883267";
            const string s2 = @"SongId={008024a9-d610-4d60-a60d-d168261e25fc}	.Create=	User=DWTS	Time=04/14/2015 12:10:41	Title=Alice's Theme	Artist=Danny Elfman	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 5:Other|Season 20:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Mark:Other|Willow:Other	Album:00=Alice in Wonderland	.Edit=	User=batch-a	Time=4/14/2015 12:14:34 PM	Length=307	Album:00=Alice In Wonderland	Track:00=1	Purchase:00:AS=D:B0039F4QBW	Purchase:00:AA=D:B0039F67GE	Tag+=childrens:Music	.Edit=	User=batch-i	Time=4/14/2015 12:14:34 PM	Length=308	Album:01=Alice In Wonderland (Original Soundtrack)	Track:01=1	Purchase:01:IS=358589687	Purchase:01:IA=358589663	Album:02=Almost Alice (Deluxe Version)	Track:02=18	Purchase:02:IS=373383374	Purchase:02:IA=373383164	Album:03=A Musical Tour: Treasures of the Walt Disney Archives at The Reagan Library	Track:03=9	Purchase:03:IS=537797522	Purchase:03:IA=537797509	Album:04=Almost Alice (Music Inspired By the Motion Picture)	Track:04=18	Purchase:04:IS=356532350	Purchase:04:IA=356532177	Tag+=Soundtrack:Music	.Edit=	User=batch-s	Time=4/14/2015 12:14:35 PM	Title=Alice's Theme (from ""Alice in Wonderland"")	Length=307	Album:00=Alice in Wonderland	Purchase:00:SS=4p7EYvJ6yWEjZDZuguSti7	Purchase:00:SA=4JXo4nvXKYbUTGbG7md0P8	Purchase:03:SS=73zKA4MtUsNa2QIPZ8ZBHG	Purchase:03:SA=0GHAq8ItjkL8xRGf9bM7s9	Album:05=Almost Alice Deluxe	Track:05=18	Purchase:05:SS=7s8E1k2Bulz1rHEcnyH4dR	Purchase:05:SA=4HpY9p5VuX6yQm2DroaGRI	.FailedLookup=-:0	.Edit=	User=batch	Time=4/14/2015 12:24:28 PM	Tempo=120.4	Album:06=Alice's Theme From The Motion Picture ""Alice In Wonderland"" By Danny Elfman	Track:06=1	Purchase:06:XS=music.E5F81D06-0100-11DB-89CA-0019B92A3933	Tag+=Slow Foxtrot:Dance|Soundtracks:Music	DanceRating=SFT+4	DanceRating=FXT+1	DanceRating=SFT";
            const string s3 = @"SongId={25053e8c-5f1e-441e-bd54-afdab5b1b638}	.Create=	User=LucyM	Time=05/07/2015 18:23:24	Title=When a Man Loves a Woman	Artist=Percy Sledge	Tag+=First Dance:Other|Slow Waltz:Dance|Wedding:Other	DanceRating=SWZ+3	DanceRating=WLZ+1	.Edit=	User=batch-a	Time=5/7/2015 6:26:18 PM	Title=When A Man Loves A Woman	Length=173	Album:00=The Ultimate Performance - When A Man Loves A Woman	Track:00=2	Purchase:00:AS=D:B000S3I57O	Purchase:00:AA=D:B000S9AJS6	Album:01=The Ultimate Collection - When A Man Loves A Woman	Track:01=1	Purchase:01:AS=D:B002YS1DKC	Purchase:01:AA=D:B002YRZA5C	Album:02=22 All Time Greatest Hits	Track:02=1	Purchase:02:AS=D:B0042NJ21E	Purchase:02:AA=D:B0042NJ20U	Album:03=When A Man Loves A Woman	Track:03=1	Purchase:03:AS=D:B000S3AO3W	Purchase:03:AA=D:B000S9AK7Q	Album:04=Best Of Percy Sledge	Track:04=1	Purchase:04:AS=D:B000VK58AQ	Purchase:04:AA=D:B000VKAZTK	Album:05=When A Man Loves A Woman (US Release)	Track:05=1	Purchase:05:AS=D:B00124HN8K	Purchase:05:AA=D:B00124HSFS	Tag+=r-b:Music|rock:Music	.Edit=	User=batch-i	Time=5/7/2015 6:26:19 PM	Title=When A Man Loves A Woman (Re-Recorded / Remastered)	Length=168	Album:01=The Ultimate Collection - When a Man Loves a Woman	Purchase:01:IS=342422884	Purchase:01:IA=342422878	Purchase:03:IS=370626514	Purchase:03:IA=370626303	Album:06=Brothers In Blues & Sisters In Soul - 18 Classic Tracks (Re-Recorded Version)	Track:06=17	Purchase:06:IS=15146791	Purchase:06:IA=15146793	Album:07=Greatest Valentine Love Songs: 50s-80s (Re-Recorded Versions)	Track:07=11	Purchase:07:IS=213169511	Purchase:07:IA=213169373	Album:08=20 Timeless Love Songs of the Sixties - Love Is All Around (Re-Recorded Versions)	Track:08=10	Purchase:08:IS=259119364	Purchase:08:IA=259118472	Album:09=Memories - The Greatest Love Songs of the Sixties (Re-Recorded Version)	Track:09=9	Purchase:09:IS=18454502	Purchase:09:IA=18454515	Album:10=Unchained Melodies: 18 Songs that will Live Forever (Re-Recorded Version)	Track:10=2	Purchase:10:IS=19280339	Purchase:10:IA=19280368	Album:11=The World's Greatest Love Songs: 16 Unforgettable Million Sellers (Re-Recorded Versions)	Track:11=4	Purchase:11:IS=26105764	Purchase:11:IA=26105799	Album:12=So Much In Love (Re-Recorded Version)	Track:12=2	Purchase:12:IS=25511925	Purchase:12:IA=25512004	Album:13=Sex In the City (Re-Recorded Versions)	Track:13=4	Purchase:13:IS=277479883	Purchase:13:IA=277479812	Album:14=The Number 1 Soul Collection	Track:14=4	Purchase:14:IS=18454603	Purchase:14:IA=18454639	Album:15=Greatest Valentine Love Songs of the 60's (Re-Recorded Versions)	Track:15=9	Purchase:15:IS=212525433	Purchase:15:IA=212520582	Album:16=Pop Hits of the 60's & 70's - 18 Great Memories (Re-Recorded Versions)	Track:16=17	Purchase:16:IS=253605121	Purchase:16:IA=253601771	Album:17=Wonderful World of the 60's - 100 Hit Songs (Re-Recorded Versions)	Track:17=5	Purchase:17:IS=282571425	Purchase:17:IA=282570804	Album:18=Golden Legends: R&B Ballads (Re-Recorded Versions)	Track:18=2	Purchase:18:IS=128649105	Purchase:18:IA=128649088	Album:19=101 Essential Sixties Classics (Re-Recorded Versions)	Track:19=13	Purchase:19:IS=353372748	Purchase:19:IA=353372480	Album:20=Wedding Essentials: Reception Party, Vol 1	Track:20=2	Purchase:20:IS=325616499	Purchase:20:IA=325616382	Album:21=Be My Valentine - R&B's Greatest Love Songs	Track:21=18	Purchase:21:IS=302952163	Purchase:21:IA=302952141	Album:22=You Belong To Me	Track:22=13	Purchase:22:IS=358930273	Purchase:22:IA=358930251	Album:23=23 Essential Soul Masters and Ballads (Re-Recorded Versions)	Track:23=1	Purchase:23:IS=218210249	Purchase:23:IA=218210242	Album:24=Quiet Storm - Soulful Slow Jams (Rerecorded Version)	Track:24=4	Purchase:24:IS=514998708	Purchase:24:IA=514997897	Album:25=100 Timeless Love Songs (Re-Recorded Versions)	Track:25=1	Purchase:25:IS=337871972	Purchase:25:IA=337871913	Album:26=The Songs That Made Them Famous (Re-Recorded Versions)	Track:26=7	Purchase:26:IS=218852255	Purchase:26:IA=218851964	Album:27=A Soulful Valentines (Re-recorded Version)	Track:27=1	Purchase:27:IS=271636020	Purchase:27:IA=271635992	Album:28=The Definitive Love Collection (Re-Recorded Versions)	Track:28=3	Purchase:28:IS=265859918	Purchase:28:IA=265859266	Album:29=#1 Hits of the 60's (Re-Recorded Versions)	Track:29=9	Purchase:29:IS=334347101	Purchase:29:IA=334347030	Album:30=It's Soul Time!	Track:30=8	Purchase:30:IS=394849617	Purchase:30:IA=394849217	Album:31=20th Century Rocks: 60's Soul - Tell It Like It Is	Track:31=2	Purchase:31:IS=334382487	Purchase:31:IA=334382421	Album:32=150 Rock 'N' Roll Classics (Re-Recorded Versions)	Track:32=8	Purchase:32:IS=336322879	Purchase:32:IA=336321874	Album:33=The Roots of Alicia Keys	Track:33=13	Purchase:33:IS=274335400	Purchase:33:IA=274333361	Album:34=Broken Hearted - 18 Classic Tearjerkers (Re-recorded Version)	Track:34=14	Purchase:34:IS=265821134	Purchase:34:IA=265818216	Album:35=A Box Full of Love, Vol. 3 (Re-recorded Version)	Track:35=2	Purchase:35:IS=266411962	Purchase:35:IA=266411801	Album:36=I Love the 60's - 1966	Track:36=7	Purchase:36:IS=300263344	Purchase:36:IA=300263335	Album:37=36 Soul Classics	Track:37=3	Purchase:37:IS=334964925	Purchase:37:IA=334964780	Album:38=30 Soul Classics	Track:38=1	Purchase:38:IS=335447187	Purchase:38:IA=335447154	Album:39=Just One Look - 16 Songs of Love (Rerecorded Version)	Track:39=13	Purchase:39:IS=336874249	Purchase:39:IA=336874088	Album:40=S.O.S. Summer of Soul - Beach Party Forever	Track:40=19	Purchase:40:IS=972573919	Purchase:40:IA=972573561	Album:41=Soul	Track:41=28	Purchase:41:IS=947040591	Purchase:41:IA=947040414	Album:42=Atlantic 60th - Love Song Soul	Track:42=8	Purchase:42:IS=266611467	Purchase:42:IA=266610607	Album:43=Slow Jams (Re-Recorded Version)	Track:43=15	Purchase:43:IS=283902895	Purchase:43:IA=283902834	Album:44=Oldies But Goldies (Rerecorded Version)	Track:44=1	Purchase:44:IS=813609028	Purchase:44:IA=813609003	Album:45=Ultimate '70s Soul Sensations (Re-Recorded / Remastered Versions)	Track:45=12	Purchase:45:IS=309791141	Purchase:45:IA=309790955	Album:46=Lazy Romance	Track:46=5	Purchase:46:IS=539175983	Purchase:46:IA=539175975	Album:47=#1 Pop Hits of the 60s & 70s (Digital Version) [Re-Recorded Versions]	Track:47=20	Purchase:47:IS=140188901	Purchase:47:IA=140187079	Album:48=Best of the Best: Percy Sledge & Dobie Gray (Re-Recorded Versions)	Track:48=1	Purchase:48:IS=212753006	Purchase:48:IA=212752954	Album:49=22 All-Time Greatest Hits (Re-Recorded Versions)	Track:49=1	Purchase:49:IS=219410207	Purchase:49:IA=219410197	Album:50=Soul Box	Track:50=6	Purchase:50:IS=268077480	Purchase:50:IA=268075621	Album:51=Why Do Fools Fall in Love? 25 Golden Oldies Love Songs by Doris Day, Patsy Cline, Roy Orbison, The Platters, Righteous Brothers & More!	Track:51=10	Purchase:51:IS=806806578	Purchase:51:IA=806806511	Album:52=Golden Oldies Vol 3	Track:52=10	Purchase:52:IS=423441819	Purchase:52:IA=423441618	Album:53=The Country Side of Percy Sledge and Arthur Prysock (Original Gusto Recordings)	Track:53=1	Purchase:53:IS=563981927	Purchase:53:IA=563981880	Album:54=60's Pop Gold Vol. 2	Track:54=12	Purchase:54:IS=360998196	Purchase:54:IA=360997936	Album:55=My Girl Soul Hits of Love (with Various Artists)	Track:55=2	Purchase:55:IS=720228538	Purchase:55:IA=720228496	Album:56=R&B Mega-Hits of the 1960s, Vol. 2	Track:56=6	Purchase:56:IS=287701239	Purchase:56:IA=287701054	Album:57=The Best of Percy Sledge	Track:57=1	Purchase:57:IS=342386193	Purchase:57:IA=342386143	Album:58=Rhino Hi-Five: Percy Sledge - EP	Track:58=2	Purchase:58:IS=145082079	Purchase:58:IA=145082424	Album:59=Rhino Hi-Five: Wedding Songs 1 - EP	Track:59=3	Purchase:59:IS=63820558	Purchase:59:IA=63820564	Album:60=Only In America: Atlantic Soul Classics	Track:60=48	Purchase:60:IS=310526934	Purchase:60:IA=310526727	Album:61=Atlantic Top 60: Sweat-Soaked Soul Classics	Track:61=31	Purchase:61:IS=266634379	Purchase:61:IA=266633305	Album:62=When a Man Loves a Woman / Love Me Like You Mean It [Digital 45] - Single	Track:62=1	Purchase:62:IS=329942646	Purchase:62:IA=329942333	Album:63=#1 Hits of the 50s & 60s	Track:63=24	Purchase:63:IS=331809318	Purchase:63:IA=331807423	Album:64=Golden Legends: Soul Legends (Re-Recorded Version)	Track:64=9	Purchase:64:IS=129017944	Purchase:64:IA=129017678	Album:65=Atlantic 60th: Soul, Sweat and Strut	Track:65=5	Purchase:65:IS=266608927	Purchase:65:IA=266608572	Album:66=Soul Men! Their Greatest Hits (Re-Recorded Versions)	Track:66=1	Purchase:66:IS=204784104	Purchase:66:IA=204784100	Album:67=Soul Train Live Vol. 2	Track:67=14	Purchase:67:IS=283900567	Purchase:67:IA=283900476	Album:68=100 Essential Funk & Soul Hits Live	Track:68=10	Purchase:68:IS=523044814	Purchase:68:IA=523044187	Album:69=Atlantic Rhythm & Blues 1947-1974	Track:69=10	Purchase:69:IS=917202622	Purchase:69:IA=917202190	Album:70=Super Hits 1966 (Re-Recorded Versions)	Track:70=1	Purchase:70:IS=210390667	Purchase:70:IA=210390639	Album:71=Soul Brothers & Soul Sisters (Re-recorded Version)	Track:71=3	Purchase:71:IS=250385803	Purchase:71:IA=250385652	Album:72=Super Hits of the 1960s (Re-Recorded Versions)	Track:72=13	Purchase:72:IS=286996698	Purchase:72:IA=286995923	Album:73=The Greatest Soul Hits of All Time Vol. 2	Track:73=13	Purchase:73:IS=532445630	Purchase:73:IA=532445407	Album:74=80 R&B Hits	Track:74=42	Purchase:74:IS=420950832	Purchase:74:IA=420950369	Album:75=100 Soul Hits	Track:75=9	Purchase:75:IS=881894782	Purchase:75:IA=881894722	Album:76=Music & Highlights: The Greatest Soul Hits	Track:76=3	Purchase:76:IS=735963319	Purchase:76:IA=735963168	Album:77=R&B from the 60's	Track:77=1	Purchase:77:IS=303489733	Purchase:77:IA=303489730	Album:78=Big 60s Hits Volume 6	Track:78=1	Purchase:78:IS=322164623	Purchase:78:IA=322164292	Album:79=60s Gold	Track:79=9	Purchase:79:IS=290815548	Purchase:79:IA=290815528	Album:80=100 Hits - '60s & '70s (Re-Recorded / Remastered Versions)	Track:80=13	Purchase:80:IS=355227184	Purchase:80:IA=355226779	Album:81=The Greatest '60s Gold Collection	Track:81=8	Purchase:81:IS=408680528	Purchase:81:IA=408680446	Album:82=The Big Chill - 15th Anniversary (More Songs from the Original Soundtrack) [Soundtrack from the Motion Picture]	Track:82=4	Purchase:82:IS=879641665	Purchase:82:IA=3453896	Album:83=Platinum & Gold The '60s	Track:83=10	Purchase:83:IS=397972089	Purchase:83:IA=397971700	Album:84=Good Morning Vietnam	Track:84=10	Purchase:84:IS=357287394	Purchase:84:IA=357287162	Album:85=Animal House - 100 Rock N' Roll Classics Of The '50s & '60s	Track:85=6	Purchase:85:IS=403036345	Purchase:85:IA=403036142	Album:86=More Solid Gold 60s Volume 2	Track:86=6	Purchase:86:IS=330201086	Purchase:86:IA=330200794	Album:87=Good Morning Vietnam - Music & Words Of The '60s	Track:87=25	Purchase:87:IS=324079492	Purchase:87:IA=324078436	Album:88=Jersey Days - Boys & Girls That Rocked (Re-Recorded Versions)	Track:88=17	Purchase:88:IS=259187182	Purchase:88:IA=259185053	Album:89=1960s Soul Movement - 60 Hits Of The '60s (Re-Recorded / Remastered Versions)	Track:89=11	Purchase:89:IS=420584900	Purchase:89:IA=420584885	Album:90=Soundtrack To The '60s  (Re-Recorded / Remastered Versions)	Track:90=6	Purchase:90:IS=355415040	Purchase:90:IA=355414997	Album:91=China Beach (Music Inspired By The Television Series)	Track:91=20	Purchase:91:IS=358338786	Purchase:91:IA=358337981	Album:92=Rhythm, Soul & Love Big Hits of R&B 60s Collection	Track:92=1	Purchase:92:IS=725562257	Purchase:92:IA=725562150	Album:93=Classic Power Ballads	Track:93=40	Purchase:93:IS=642216620	Purchase:93:IA=642215949	Album:94=Pure Love Moods Vol. 1	Track:94=2	Purchase:94:IS=267389126	Purchase:94:IA=267387789	Album:95=Golden Oldies Forever	Track:95=1	Purchase:95:IS=396815542	Purchase:95:IA=396815312	Album:96=Bubble Gum Hits Of The '60s & '70s (Re-Recorded / Remastered Versions)	Track:96=25	Purchase:96:IS=357219378	Purchase:96:IA=357218955	Album:97=60s Classics (Re-Recorded Versions)	Track:97=9	Purchase:97:IS=259239928	Purchase:97:IA=259239305	Album:98=Top Ten Hits of the Sixties, Vol. 1 (Re-Recorded Versions)	Track:98=4	Purchase:98:IS=252697248	Purchase:98:IA=252697163	Album:99=The Sixties: 40 Big Hits!	Track:99=1	Purchase:99:IS=384852684	Purchase:99:IA=384852659	Album:100=I Love the 60s	Track:100=7	Purchase:100:IS=441592891	Purchase:100:IA=441592721	Album:101=Sweet Soul '70s Magic (Re-Recorded / Remastered Versions)	Track:101=16	Purchase:101:IS=348281029	Purchase:101:IA=348278360	Album:102=You Really Got Me! - 60s Solid Gold	Track:102=7	Purchase:102:IS=473120857	Purchase:102:IA=473120850	Album:103=Classic Soul Ballads (Re-recorded Version)	Track:103=2	Purchase:103:IS=339209110	Purchase:103:IA=339208830	Album:104=Absolutely The Best Of The Sixties	Track:104=18	Purchase:104:IS=371843064	Purchase:104:IA=371842976	Album:105=Funk & Soul Essentials	Track:105=5	Purchase:105:IS=338056282	Purchase:105:IA=338056212	Album:106=Back To The 50's & 60's	Track:106=10	Purchase:106:IS=405019408	Purchase:106:IA=405019386	Album:107=Solid Gold: The 60's Collection	Track:107=5	Purchase:107:IS=521858761	Purchase:107:IA=521858708	Album:108=Love...'60s Style	Track:108=5	Purchase:108:IS=379811287	Purchase:108:IA=379811275	Album:109=Love Songs	Track:109=1	Purchase:109:IS=265732399	Purchase:109:IA=265732389	Album:110=My Girl: Solid Gold 60s Hits	Track:110=1	Purchase:110:IS=467136027	Purchase:110:IA=467136025	Album:111=The Ultimate Black History Collection	Track:111=37	Purchase:111:IS=331461484	Purchase:111:IA=331461031	Album:112=Platoon (Music Inspired By The Film)	Track:112=1	Purchase:112:IS=357273043	Purchase:112:IA=357273021	Album:113=Gold '60s Explosion	Track:113=12	Purchase:113:IS=397993689	Purchase:113:IA=397993532	Album:114=Sexy Love	Track:114=1	Purchase:114:IS=367214017	Purchase:114:IA=367213999	Album:115=Valentine's Day Love Forever	Track:115=6	Purchase:115:IS=415565144	Purchase:115:IA=415565119	Album:116=Absolutely The Best Of AM Radio: 60's Edition	Track:116=21	Purchase:116:IS=456643740	Purchase:116:IA=456643638	Album:117=Guilty Pleasures (Re-Recorded / Remastered Versions)	Track:117=8	Purchase:117:IS=355227180	Purchase:117:IA=355226037	Album:118=#1 60s Gold (Re-recorded Version)	Track:118=5	Purchase:118:IS=321000298	Purchase:118:IA=320999989	Album:119=The Ultimate '60s Collection	Track:119=20	Purchase:119:IS=372905363	Purchase:119:IA=372905243	Album:120=Mind Blowing '60s Rock Classics	Track:120=6	Purchase:120:IS=327693218	Purchase:120:IA=327692985	Album:121=Sugar Sugar and More #1 Hits	Track:121=5	Purchase:121:IS=422661582	Purchase:121:IA=422661565	Album:122=100 '60s Hits (Re-Recorded Version) [Remastered]	Track:122=10	Purchase:122:IS=553519386	Purchase:122:IA=553519261	Album:123=Back To The '60s	Track:123=16	Purchase:123:IS=414633377	Purchase:123:IA=414633285	Album:124=Golden Hits Volume 3	Track:124=10	Purchase:124:IS=337942798	Purchase:124:IA=337942608	Album:125=Sexy 60s Solid Gold	Track:125=6	Purchase:125:IS=468322729	Purchase:125:IA=468322700	Album:126=Sex In The City (Re-Recorded / Remastered Versions)	Track:126=5	Purchase:126:IS=376726805	Purchase:126:IA=376726124	Album:127=The Greatest Love Songs Of All Time	Track:127=1	Purchase:127:IS=359838794	Purchase:127:IA=359838788	Album:128=Super Ballads (Re-Recorded / Remastered Versions)	Track:128=40	Purchase:128:IS=450793192	Purchase:128:IA=450791877	Album:129=Happy Sounds Of The '60s (Re-Recorded Versions)	Track:129=1	Purchase:129:IS=357261373	Purchase:129:IA=357260926	Album:130=50 #1 Hits (Re-Recorded / Remastered Versions)	Track:130=1	Purchase:130:IS=326617584	Purchase:130:IA=326617157	Tag+=Blues:Music|Country:Music|Funk:Music|Oldies:Music|Pop:Music|R&B/Soul:Music|Rock & Roll:Music|Rock:Music|Soul:Music|Vocal:Music	.Edit=	User=batch-x	Time=5/7/2015 6:26:20 PM	Length=172	Purchase:45:XS=music.CD16F506-0100-11DB-89CA-0019B92A3933	Album:100=I Love The 60s	Purchase:100:XS=music.1135D006-0100-11DB-89CA-0019B92A3933	Purchase:120:XS=music.14C98106-0100-11DB-89CA-0019B92A3933	Album:131=Soul Giants - Greatest Soul Collection	Track:131=13	Purchase:131:XS=music.3E7A5C07-0100-11DB-89CA-0019B92A3933	Album:132=100 Greatest Motown Hits	Track:132=8	Purchase:132:XS=music.D7095607-0100-11DB-89CA-0019B92A3933	Album:133=Motown Soul Essentials	Track:133=1	Purchase:133:XS=music.21B24B07-0100-11DB-89CA-0019B92A3933	Album:134=When A Man Loves A Woman - Greatest Hits	Track:134=1	Purchase:134:XS=music.9911DF01-0100-11DB-89CA-0019B92A3933	Album:135=The Roots Of Cee Lo Green	Track:135=19	Purchase:135:XS=music.A103E006-0100-11DB-89CA-0019B92A3933	Album:136=69 Top Hits @ 69¢	Track:136=56	Purchase:136:XS=music.64B50C07-0100-11DB-89CA-0019B92A3933	Album:137=100 Radio Hits Of The '60s, '70s, '80s & '90s	Track:137=24	Purchase:137:XS=music.AC4C7506-0100-11DB-89CA-0019B92A3933	Album:138=Music Inspired By Dirty Dancing	Track:138=15	Purchase:138:XS=music.CD4F7706-0100-11DB-89CA-0019B92A3933	Album:139=100 '60s Hits	Track:139=10	Purchase:139:XS=music.E70D7506-0100-11DB-89CA-0019B92A3933	Album:140=Swinging With Tiger Woods	Track:140=35	Purchase:140:XS=music.92537706-0100-11DB-89CA-0019B92A3933	Album:141=Bubble Gum Hits Of The '60s & '70s	Track:141=25	Purchase:141:XS=music.0E2F1D06-0100-11DB-89CA-0019B92A3933	Album:142=Super Ballads	Track:142=80	Purchase:142:XS=music.98C0B306-0100-11DB-89CA-0019B92A3933	Album:143=Guilty Pleasures	Track:143=38	Purchase:143:XS=music.FE021B06-0100-11DB-89CA-0019B92A3933	Album:144=Songs From Tv Commercials	Track:144=18	Purchase:144:XS=music.D5038406-0100-11DB-89CA-0019B92A3933	Album:145=The Greatest Singers Of All Time	Track:145=27	Purchase:145:XS=music.1F820C02-0100-11DB-89CA-0019B92A3933	Album:146=Romantic Valentines Day	Track:146=1	Purchase:146:XS=music.FFB6B006-0100-11DB-89CA-0019B92A3933	Album:147=Sex In The City	Track:147=5	Purchase:147:XS=music.602D2706-0100-11DB-89CA-0019B92A3933	Album:148=100 Slow Jams	Track:148=24	Purchase:148:XS=music.158F2506-0100-11DB-89CA-0019B92A3933	Album:149=50 #1 Hits	Track:149=1	Purchase:149:XS=music.3B7EEF01-0100-11DB-89CA-0019B92A3933	Tag+=Pop:Music|R&B / Soul:Music|Rock:Music	.FailedLookup=-:0";
            var songs = new List<string>(new[] {s1,s2,s3});
            var dmt = new DanceMusicTester(songs);

            var ots = dmt.Dms.Tags.ToList();

            dmt.Dms.RebuildUserTags("batch",true);

            var nts = dmt.Dms.Tags.ToList();
            
            Assert.AreEqual(ots.Count,nts.Count);
            for (var i = 0; i < ots.Count; i++)
            {
                Assert.AreEqual(ots[i].ToString(),nts[i].ToString());
            }
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

        private static readonly string[] VerifiesInit = {
            "",
            "Blues:dance|Pop:music",
            "4/4:tempo|fast:tempo",
            "Crazy|Christmas:Christmas",
        };

        private static readonly string[] VerifiesResult = {
            "",
            "Blues:Dance|Pop:Music",
            "4/4:Tempo|fast:Tempo",
            "Christmas:Other|Crazy:Other",
        };

        static readonly DanceMusicService Service;

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
