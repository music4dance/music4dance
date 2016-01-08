using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNet.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongTests
    {
        [TestMethod]
        public void NormalForm()
        {
            for (var i = 0; i < Titles.Length; i++)
            {
                var t = Titles[i];

                var n = SongBase.CreateNormalForm(t);
                Assert.AreEqual(Normal[i], n);
                //Trace.WriteLine(string.Format("{0}",Song.CreateTitleHash(t)));
            }
        }

        [TestMethod]
        public void TitleHash()
        {
            for (var i = 0; i < Hashes.Length; i++)
            {
                var t = Titles[i];

                var hash = SongBase.CreateTitleHash(t);
                Assert.AreEqual(Hashes[i], hash);
            }
        }

        [TestMethod]
        public void NormalString()
        {
            for (var i = 0; i < Titles.Length; i++)
            {
                var t = Titles[i];

                var n = SongBase.CreateNormalForm(t);
                Assert.AreEqual(Normal[i], n);
                //Trace.WriteLine(string.Format("{0}",Song.CreateTitleHash(t)));
            }
        }

        [TestMethod]
        public void CleanString()
        {
            for (var i = 0; i < Titles.Length; i++)
            {
                var t = Titles[i];

                var n = SongBase.CleanString(t);
                Assert.AreEqual(Clean[i], n);
                //Trace.WriteLine(string.Format("{0}",Song.CreateTitleHash(t)));
            }
        }

        [TestMethod]
        public void AlbumFields()
        {
            Assert.IsTrue(SongBase.IsAlbumField(SongBase.AlbumField));
            Assert.IsFalse(SongBase.IsAlbumField(SongBase.TitleField));

            Assert.IsTrue(SongBase.IsAlbumField(SongBase.PublisherField + ":0"));
            Assert.IsTrue(SongBase.IsAlbumField(SongBase.TrackField + ":45:A"));

            Assert.IsFalse(SongBase.IsAlbumField(null));
            Assert.IsFalse(SongBase.IsAlbumField("#"));
        }

        [TestMethod]
        public void Ratings()
        {
            var song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0",Service);

            //Trace.WriteLine(init);
            var sd = new SongDetails(song);

            sd.UpdateDanceRatings(new[] {"RMB","CHA"}, 5);
            sd.UpdateDanceRatings(new[] { "FXT" }, 7);

            // Create an test an initial small list of dance ratings
            var user = Service.FindUser("dwgray");
            song.Update(user, sd, Service);
            //var first = song.ToString();
            //Trace.WriteLine(first);
            Assert.IsTrue(song.DanceRatings.Count == 3);

            // Now mix it up a bit

            sd.UpdateDanceRatings(new[] { "RMB", "FXT" }, 3);
            song.Update(user, sd, Service);
            Assert.IsTrue(song.DanceRatings.Count == 3);
            var drT = song.FindRating("RMB");
            Assert.IsTrue(drT.Weight == 8);

            sd.UpdateDanceRatings(new[] { "CHA", "FXT" }, -5);
            song.Update(user, sd, Service);
            Assert.IsTrue(song.DanceRatings.Count == 2);
            drT = song.FindRating("FXT");
            Assert.IsTrue(drT.Weight == 5);
        }

        private const string MergeSong =
            @".Create=	User=batch	Time=04/15/2015 21:15:47	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Tempo=123.0	Length=164	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	DanceRating=FXT+1	Tag+:SFT=Contemporary:Style	.Edit=	User=batch-a	Time=4/15/2015 9:27:05 PM	Title=Wouldn't It Be Nice (2000 - Remaster)	Length=153	Album:00=The Pet Sounds Sessions: A 30th Anniversary Collection	Track:00=23	Purchase:00:AS=D:B000T2M00W	Purchase:00:AA=D:B000T2KFKO	Album:01=The Very Best Of The Beach Boys: Sounds Of Summer	Track:01=16	Purchase:01:AS=D:B000TDUV0C	Purchase:01:AA=D:B000TETD9Q	Album:02=Pet Sounds 40th Anniversary Stereo Digital	Track:02=1	Purchase:02:AS=D:B000T060LE	Purchase:02:AA=D:B000T06172	Album:03=50 Big Ones: Greatest Hits	Track:03=24	Purchase:03:AS=D:B009D0IAAA	Purchase:03:AA=D:B009D0Q5PM	Album:04=Pet Sounds	Track:04=15	Purchase:04:AS=D:B000SNW7IM	Purchase:04:AA=D:B000SZZIH2	Tag+=pop:Music|rock:Music	.Edit=	User=batch-i	Time=4/15/2015 9:27:05 PM	Title=Wouldn't It Be Nice (2000 Remaster)	Album:05=Summer Love Songs	Track:05=3	Purchase:05:IS=723863447	Purchase:05:IA=723863135	Tag+=Rock:Music	.Edit=	User=batch-x	Time=4/15/2015 9:27:07 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=DWTS	Time=09/23/2015 16:00:15	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 2:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Anna:Other|Gary:Other";
        [TestMethod]
        public void AdditiveMerge()
        {
            var song = new Song();
            song.Load(MergeSong, Service);
            Service.Songs.Add(song);

            var user = Service.UserManager.FindByName("dwgray");
            var header = new List<string> { "Title", "Artist", "DanceRating", "DanceTags:Style", "SongTags:Other"};
            var row = new List<string> { @"Would It Not Be Nice	Beach Boys	Swing	Modern	Wedding" };
            var merge = SongDetails.CreateFromRows(user, "\t", header, row, SongBase.DanceRatingIncrement)[0];
            merge.Tempo = 123;
            merge.InferDances(user);

            var changed = Service.AdditiveMerge(user, song.SongId, merge, null);
            Assert.IsTrue(changed);

            const string expected = @".Create=	User=batch	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Tempo=123.0	Length=164	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	DanceRating=FXT+1	Tag+:SFT=Contemporary:Style	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 - Remaster)	Length=153	Album:00=The Pet Sounds Sessions: A 30th Anniversary Collection	Track:00=23	Purchase:00:AS=D:B000T2M00W	Purchase:00:AA=D:B000T2KFKO	Album:01=The Very Best Of The Beach Boys: Sounds Of Summer	Track:01=16	Purchase:01:AS=D:B000TDUV0C	Purchase:01:AA=D:B000TETD9Q	Album:02=Pet Sounds 40th Anniversary Stereo Digital	Track:02=1	Purchase:02:AS=D:B000T060LE	Purchase:02:AA=D:B000T06172	Album:03=50 Big Ones: Greatest Hits	Track:03=24	Purchase:03:AS=D:B009D0IAAA	Purchase:03:AA=D:B009D0Q5PM	Album:04=Pet Sounds	Track:04=15	Purchase:04:AS=D:B000SNW7IM	Purchase:04:AA=D:B000SZZIH2	Tag+=pop:Music|rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 Remaster)	Album:05=Summer Love Songs	Track:05=3	Purchase:05:IS=723863447	Purchase:05:IA=723863135	Tag+=Rock:Music	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 2:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Anna:Other|Gary:Other	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Swing:Dance|Wedding:Other	DanceRating=SWG+2	DanceRating=CSG+1	DanceRating=WCS+1	DanceRating=LHP+1	Tag+:SWG=Modern:Style";
            var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId }));
            Assert.AreEqual(expected,actual);
        }

        private static readonly string[] AlbumSongs = {
            @"User=dwgray	OwnerHash=-1863299954	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Artist=The Doors	Tempo=119.8	Length=131	Album:00=Best Of The Doors [Disc 1]	Track:00=4	Publisher:00=WEA	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 7:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Tamar:Other|Val:Other	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Length=130	Album:01=Strange Days	Track:01=7	Purchase:01:AS=D:B0018AO430	Purchase:01:AA=D:B0018ARNR4	Album:02=The Very Best Of [w/bonus tracks]	Track:02=10	Purchase:02:AS=D:B001232CDW	Purchase:02:AA=D:B00122X4JE	Album:03=When You're Strange (Songs From The Motion Picture)	Track:03=15	Purchase:03:AS=D:B003ELZ52K	Purchase:03:AA=D:B003EM54IE	Album:04=The Complete Studio Albums	Track:04=7	Purchase:04:AS=D:B00H7L85VQ	Purchase:04:AA=D:B00H7L7WOM	Album:05=The Future Starts Here: The Essential Doors Hits	Track:05=5	Purchase:05:AS=D:B0012QLOVU	Purchase:05:AA=D:B0012QK80S	Tag+=classic-rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=People Are Strange (Live At the Matrix)	Length=135	Purchase:01:IS=640074407	Purchase:01:IA=640074114	Purchase:05:IS=641333125	Purchase:05:IA=641332994	Album:06=When You're Strange (Songs from the Motion Picture) [Deluxe Version]	Track:06=15	Purchase:06:IS=363701078	Purchase:06:IA=363700945	Album:07=The Very Best of the Doors	Track:07=10	Purchase:07:IS=640047588	Purchase:07:IA=640047463	Album:08=Live At the Matrix	Track:08=17	Purchase:08:IS=639676770	Purchase:08:IA=639676554	Tag+=Rock:Music|Soundtrack:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Title=People Are Strange [Live At The Matrix]	Album:09=Strange Nights Of Stone	Track:09=2017	Purchase:09:SS=1VbzTlxsJdW1HFU8rdDhE4	Purchase:09:SA=7bs3oHD0CaOQxUSdjr3osB	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Length=130	Purchase:01:XS=music.89E16C00-0100-11DB-89CA-0019B92A3933	Purchase:01:MS=T  1532957	Track:04=18	Purchase:04:XS=music.3AB30408-0100-11DB-89CA-0019B92A3933	Track:09=33	Purchase:09:XS=music.EB2FB307-0100-11DB-89CA-0019B92A3933	Purchase:09:MS=T 30439516	Album:10=The Future Starts Here: The Essential Doors Hits (New Stereo Mix)	Track:10=5	Purchase:10:XS=music.AD67C100-0100-11DB-89CA-0019B92A3933	Purchase:10:MS=T 13610812	Album:11=When You're Strange	Track:11=15	Purchase:11:XS=music.7A8F1306-0100-11DB-89CA-0019B92A3933	Purchase:11:MS=T 28755028	Album:12=The Very Best Of The Doors (Bonus Tracks)	Track:12=10	Purchase:12:XS=music.8F06B000-0100-11DB-89CA-0019B92A3933	Tag+=Rock:Music	.FailedLookup=-:0",
            @"User=dwgray	Time=00/00/0000 0:00:00 PM	Title=At Last	Artist=Nat ""King"" Cole	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Artist=Nat 'King' Cole	Length=178	Album:00=Voices Of Change, Then And Now	Track:00=3	Purchase:00:AS=D:B001RJZXIC	Purchase:00:AA=D:B001RK98L4	Album:01=Things We Do At Night	Track:01=26	Purchase:01:AS=D:B00L9JKMSQ	Purchase:01:AA=D:B00L9JK600	Album:02=Songs From The Heart	Track:02=18	Purchase:02:AS=D:B001ANHP8Q	Purchase:02:AA=D:B001ANHOOG	Album:03=Love Is The Thing (Digital)	Track:03=7	Purchase:03:AS=D:B001AL1ZTS	Purchase:03:AA=D:B001AL2010	Album:04=The History of Jazz Vol. 8	Track:04=18	Purchase:04:AS=D:B00TJ6JF3Y	Purchase:04:AA=D:B00TJ6H25C	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Artist=Nat ""King"" Cole	Length=180	Album:05=Voices of Change, Then and Now - EP	Track:05=3	Purchase:05:IS=716435737	Purchase:05:IA=716435712	Album:06=Songbook Series: Songs from the Heart	Track:06=18	Purchase:06:IS=724732281	Purchase:06:IA=724731774	Album:07=Love Is the Thing (And More)	Track:07=7	Purchase:07:IS=724954084	Purchase:07:IA=724951463	Tag+=Pop:Music|Vocal Pop:Music|Vocal:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Artist=Nat King Cole	Album:08=Love Is The Thing	Track:08=7	Purchase:08:SS=6pLzRKiNpmNz53Eism3vyr	Purchase:08:SA=0M74fKKEBEFUSmiGbjIkps	Album:09=The Unforgettable Nat King Cole	Track:09=2018	Purchase:09:SS=5VknVSEGCZaCBa3IfkTzUw	Purchase:09:SA=2GbhcTRu7BCPkss4E2YMsh	Album:10=30 Greatest Love Songs	Track:10=23	Purchase:10:SS=2iAWs4wdnilZl6pCAaTBeV	Purchase:10:SA=3Sh3Kg7mBJ0vT9yHnTUqOb	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Length=177	Purchase:00:XS=music.4E9B4A06-0100-11DB-89CA-0019B92A3933	Purchase:01:XS=music.10C76608-0100-11DB-89CA-0019B92A3933	Purchase:04:XS=music.23B6CD08-0100-11DB-89CA-0019B92A3933	Album:06=Songbook Series: Songs From The Heart	Purchase:06:XS=music.6E272B06-0100-11DB-89CA-0019B92A3933	Purchase:08:XS=music.7E584106-0100-11DB-89CA-0019B92A3933	Track:09=45	Purchase:09:XS=music.295F9D01-0100-11DB-89CA-0019B92A3933	Album:11=There Is No You	Track:11=10	Purchase:11:XS=music.E1BFFD06-0100-11DB-89CA-0019B92A3933	Tag+=Jazz:Music|More:Music|Pop:Music	.FailedLookup=-:0	.Edit=	User=batch	Time=00/00/0000 0:00:00 PM	Tempo=60.0	Tag+=Castle Foxtrot:Dance|Weak:Tempo	DanceRating=CFT+3	DanceRating=FXT+1	DanceRating=CFT+1"
        };
        private static readonly string[] AlbumExpected = {
            @"User=dwgray	OwnerHash=-1863299954	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Artist=The Doors	Tempo=119.8	Length=131	Album:00=Best Of The Doors [Disc 1]	Track:00=4	Publisher:00=WEA	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 7:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Tamar:Other|Val:Other	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Length=130	Album:01=Strange Days	Track:01=7	Purchase:01:AS=D:B0018AO430	Purchase:01:AA=D:B0018ARNR4	Album:02=The Very Best Of [w/bonus tracks]	Track:02=10	Purchase:02:AS=D:B001232CDW	Purchase:02:AA=D:B00122X4JE	Album:03=When You're Strange (Songs From The Motion Picture)	Track:03=15	Purchase:03:AS=D:B003ELZ52K	Purchase:03:AA=D:B003EM54IE	Album:04=The Complete Studio Albums	Track:04=7	Purchase:04:AS=D:B00H7L85VQ	Purchase:04:AA=D:B00H7L7WOM	Album:05=The Future Starts Here: The Essential Doors Hits	Track:05=5	Purchase:05:AS=D:B0012QLOVU	Purchase:05:AA=D:B0012QK80S	Tag+=classic-rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=People Are Strange (Live At the Matrix)	Length=135	Purchase:01:IS=640074407	Purchase:01:IA=640074114	Purchase:05:IS=641333125	Purchase:05:IA=641332994	Album:06=When You're Strange (Songs from the Motion Picture) [Deluxe Version]	Track:06=15	Purchase:06:IS=363701078	Purchase:06:IA=363700945	Album:07=The Very Best of the Doors	Track:07=10	Purchase:07:IS=640047588	Purchase:07:IA=640047463	Album:08=Live At the Matrix	Track:08=17	Purchase:08:IS=639676770	Purchase:08:IA=639676554	Tag+=Rock:Music|Soundtrack:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Title=People Are Strange [Live At The Matrix]	Album:09=Strange Nights Of Stone	Track:09=2017	Purchase:09:SS=1VbzTlxsJdW1HFU8rdDhE4	Purchase:09:SA=7bs3oHD0CaOQxUSdjr3osB	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Length=130	Purchase:01:XS=music.89E16C00-0100-11DB-89CA-0019B92A3933	Purchase:01:MS=T  1532957	Track:04=18	Purchase:04:XS=music.3AB30408-0100-11DB-89CA-0019B92A3933	Track:09=33	Purchase:09:XS=music.EB2FB307-0100-11DB-89CA-0019B92A3933	Purchase:09:MS=T 30439516	Album:10=The Future Starts Here: The Essential Doors Hits (New Stereo Mix)	Track:10=5	Purchase:10:XS=music.AD67C100-0100-11DB-89CA-0019B92A3933	Purchase:10:MS=T 13610812	Album:11=When You're Strange	Track:11=15	Purchase:11:XS=music.7A8F1306-0100-11DB-89CA-0019B92A3933	Purchase:11:MS=T 28755028	Album:12=The Very Best Of The Doors (Bonus Tracks)	Track:12=10	Purchase:12:XS=music.8F06B000-0100-11DB-89CA-0019B92A3933	Tag+=Rock:Music	.FailedLookup=-:0	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Album:02=The Very Best of the Doors	Purchase:02:IS=640047588	Purchase:02:IA=640047463	Purchase:02:XS=music.8F06B000-0100-11DB-89CA-0019B92A3933	Album:03=When You're Strange	Purchase:03:IS=363701078	Purchase:03:IA=363700945	Purchase:03:XS=music.7A8F1306-0100-11DB-89CA-0019B92A3933	Purchase:03:MS=T 28755028	Purchase:05:XS=music.AD67C100-0100-11DB-89CA-0019B92A3933	Purchase:05:MS=T 13610812	Album:06=	Track:06=	Album:07=	Track:07=	Album:10=	Track:10=	Album:11=	Track:11=	Album:12=	Track:12=",
            @"User=dwgray	Time=00/00/0000 0:00:00 PM	Title=At Last	Artist=Nat ""King"" Cole	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Artist=Nat 'King' Cole	Length=178	Album:00=Voices Of Change, Then And Now	Track:00=3	Purchase:00:AS=D:B001RJZXIC	Purchase:00:AA=D:B001RK98L4	Album:01=Things We Do At Night	Track:01=26	Purchase:01:AS=D:B00L9JKMSQ	Purchase:01:AA=D:B00L9JK600	Album:02=Songs From The Heart	Track:02=18	Purchase:02:AS=D:B001ANHP8Q	Purchase:02:AA=D:B001ANHOOG	Album:03=Love Is The Thing (Digital)	Track:03=7	Purchase:03:AS=D:B001AL1ZTS	Purchase:03:AA=D:B001AL2010	Album:04=The History of Jazz Vol. 8	Track:04=18	Purchase:04:AS=D:B00TJ6JF3Y	Purchase:04:AA=D:B00TJ6H25C	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Artist=Nat ""King"" Cole	Length=180	Album:05=Voices of Change, Then and Now - EP	Track:05=3	Purchase:05:IS=716435737	Purchase:05:IA=716435712	Album:06=Songbook Series: Songs from the Heart	Track:06=18	Purchase:06:IS=724732281	Purchase:06:IA=724731774	Album:07=Love Is the Thing (And More)	Track:07=7	Purchase:07:IS=724954084	Purchase:07:IA=724951463	Tag+=Pop:Music|Vocal Pop:Music|Vocal:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Artist=Nat King Cole	Album:08=Love Is The Thing	Track:08=7	Purchase:08:SS=6pLzRKiNpmNz53Eism3vyr	Purchase:08:SA=0M74fKKEBEFUSmiGbjIkps	Album:09=The Unforgettable Nat King Cole	Track:09=2018	Purchase:09:SS=5VknVSEGCZaCBa3IfkTzUw	Purchase:09:SA=2GbhcTRu7BCPkss4E2YMsh	Album:10=30 Greatest Love Songs	Track:10=23	Purchase:10:SS=2iAWs4wdnilZl6pCAaTBeV	Purchase:10:SA=3Sh3Kg7mBJ0vT9yHnTUqOb	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Length=177	Purchase:00:XS=music.4E9B4A06-0100-11DB-89CA-0019B92A3933	Purchase:01:XS=music.10C76608-0100-11DB-89CA-0019B92A3933	Purchase:04:XS=music.23B6CD08-0100-11DB-89CA-0019B92A3933	Album:06=Songbook Series: Songs From The Heart	Purchase:06:XS=music.6E272B06-0100-11DB-89CA-0019B92A3933	Purchase:08:XS=music.7E584106-0100-11DB-89CA-0019B92A3933	Track:09=45	Purchase:09:XS=music.295F9D01-0100-11DB-89CA-0019B92A3933	Album:11=There Is No You	Track:11=10	Purchase:11:XS=music.E1BFFD06-0100-11DB-89CA-0019B92A3933	Tag+=Jazz:Music|More:Music|Pop:Music	.FailedLookup=-:0	.Edit=	User=batch	Time=00/00/0000 0:00:00 PM	Tempo=60.0	Tag+=Castle Foxtrot:Dance|Weak:Tempo	DanceRating=CFT+3	DanceRating=FXT+1	DanceRating=CFT+1	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Purchase:00:IS=716435737	Purchase:00:IA=716435712	Album:03=Love Is The Thing	Purchase:03:IS=724954084	Purchase:03:IA=724951463	Purchase:03:SS=6pLzRKiNpmNz53Eism3vyr	Purchase:03:SA=0M74fKKEBEFUSmiGbjIkps	Purchase:03:XS=music.7E584106-0100-11DB-89CA-0019B92A3933	Album:05=	Track:05=	Album:07=	Track:07=	Album:08=	Track:08="
    };

        [TestMethod]
        public void AlbumMerge()
        {
            for (var i = 0; i < AlbumSongs.Length; i++)
            {
                var song = new Song();
                song.Load(AlbumSongs[i], Service);
                Service.Songs.Add(song);

                //Trace.WriteLine(DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId })));

                var user = Service.UserManager.FindByName("dwgray");
                Service.CleanupAlbums(user, song);

                var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId }));
                //Trace.WriteLine(actual);
                Assert.AreEqual(AlbumExpected[i], actual);
            }
        }

        [TestMethod]
        public void SpotifyCreate()
        {
            var track = new ServiceTrack()
            {
                Album = "Greatest Hits",
                Artist = "The Beach Boys",
                CollectionId = "2ninxvLuYGCb6H92qTaSFZ",
                Duration = 154,
                Name = "Wouldn't It Be Nice",
                Service = ServiceType.Spotify,
                TrackId = "6VojZJpMyuKClbwyilWlQj",
                TrackNumber = 4,
            };

            var user = Service.UserManager.FindByName("dwgray");
            var song = SongDetails.CreateFromTrack(user, track, "WCS", "Testing:Other|Crazy:Music", "Dances:Style|Mellow:Tempo");

            var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId }));
            //Trace.WriteLine(actual);

            const string expected = @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Length=154	Album:00=Greatest Hits	Track:00=4	Tag+=Crazy:Music|Testing:Other|West Coast Swing:Dance	DanceRating=WCS+2	Tag+:WCS=Dances:Style|Mellow:Tempo	Purchase:00:SA=2ninxvLuYGCb6H92qTaSFZ	Purchase:00:SS=6VojZJpMyuKClbwyilWlQj";
            Assert.AreEqual(expected,actual);
        }

        // TODO: Consider if this is a useful test (didn't end up using this code path for what it was testing and it's currently failing
        //[TestMethod]
        //public void DanceRatingMerge()
        //{
        //    var song = new Song();
        //    song.Load(MergeSong, Service);
        //    Service.Songs.Add(song);

        //    var user = Service.UserManager.FindByName("dwgray");

        //    var header = new List<string> { "Title", "Artist"};
        //    var row = new List<string> { @"Would It Not Be Nice	Beach Boys" };
        //    var merge = SongDetails.CreateFromRows(user, "\t", header, row, SongBase.DanceRatingIncrement)[0];
        //    merge.Tempo = 123;
        //    Service.UpdateDanceRatingsAndTags(merge,user,new[]{"SWG"},"Testing:Other","Modern:Style", SongBase.DanceRatingIncrement);

        //    var changed = Service.AdditiveMerge(user, song.SongId, merge, null);
        //    Assert.IsTrue(changed);

        //    const string expected = @".Create=	User=batch	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Tempo=123.0	Length=164	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	DanceRating=FXT+1	Tag+:SFT=Contemporary:Style	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 - Remaster)	Length=153	Album:00=The Pet Sounds Sessions: A 30th Anniversary Collection	Track:00=23	Purchase:00:AS=D:B000T2M00W	Purchase:00:AA=D:B000T2KFKO	Album:01=The Very Best Of The Beach Boys: Sounds Of Summer	Track:01=16	Purchase:01:AS=D:B000TDUV0C	Purchase:01:AA=D:B000TETD9Q	Album:02=Pet Sounds 40th Anniversary Stereo Digital	Track:02=1	Purchase:02:AS=D:B000T060LE	Purchase:02:AA=D:B000T06172	Album:03=50 Big Ones: Greatest Hits	Track:03=24	Purchase:03:AS=D:B009D0IAAA	Purchase:03:AA=D:B009D0Q5PM	Album:04=Pet Sounds	Track:04=15	Purchase:04:AS=D:B000SNW7IM	Purchase:04:AA=D:B000SZZIH2	Tag+=pop:Music|rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 Remaster)	Album:05=Summer Love Songs	Track:05=3	Purchase:05:IS=723863447	Purchase:05:IA=723863135	Tag+=Rock:Music	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 2:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Anna:Other|Gary:Other	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Swing:Dance|Wedding:Other	DanceRating=SWG+2	DanceRating=CSG+1	DanceRating=WCS+1	DanceRating=LHP+1	Tag+:SWG=Modern:Style";
        //    var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId }));
        //    Assert.AreEqual(expected, actual);
        //}


        static readonly string[] Titles = {
            "ñ-é á",
            "Señor  Bolero",
            "Viaje Tiemp  Atrás",
            "Solo\tTu",
            "Señales De Humo",
            "España Cañi",
            "Y'a Qu'les Filles Qui M'interessent",
            "A Namorádo",
            "Satisfaction (I Can't Get No)",
            "Satisfaction",
            "Moliendo  Café",
            "This is the Life",
            "How a Stranger can the Live",
            "Satisfaction [I Can't Get] (No)",
            "Satisfaction [I Can't Get (No)]",
            "Can't [get] [no (satisfaction)] for's real"
        };

        static readonly string[] Normal = {
            "NE",
            "SENORBOLERO",
            "VIAJETIEMPATRAS",
            "SOLOTU",
            "SENALESDEHUMO",
            "ESPANACANI",
            "YQULESFILLESQUIMINTERESSENT",
            "NAMORADO",
            "SATISFACTION",
            "SATISFACTION",
            "MOLIENDOCAFE",
            "ISLIFE",
            "HOWSTRANGERCANLIVE",
            "SATISFACTION",
            "SATISFACTION",
            "CANTFORSREAL"
        };

        static readonly string[] Clean = {
            "ñ é á",
            "Señor Bolero",
            "Viaje Tiemp Atrás",
            "Solo Tu",
            "Señales De Humo",
            "España Cañi",
            "Y'a Qu'les Filles Qui M'interessent",
            "Namorádo",
            "Satisfaction",
            "Satisfaction",
            "Moliendo Café",
            "is Life",
            "How Stranger can Live",
            "Satisfaction",
            "Satisfaction",
            "Can't for's real"
        };

        static readonly int[] Hashes = {
            -838486046,
            335748376,
            1113827306,
            2047398645,
            1119840081,
            637850480,
            -1945783477,
            2141275425,
            529734224,
            529734224,
            -1976465671,
            -1064643943,
            744080883
        };

        static readonly DanceMusicService Service = MockContext.CreateService(true);
    }
}
