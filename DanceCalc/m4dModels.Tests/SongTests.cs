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

        [TestMethod]
        public void DanceRatingMerge()
        {
            var song = new Song();
            song.Load(MergeSong, Service);
            Service.Songs.Add(song);

            var user = Service.UserManager.FindByName("dwgray");

            var header = new List<string> { "Title", "Artist"};
            var row = new List<string> { @"Would It Not Be Nice	Beach Boys" };
            var merge = SongDetails.CreateFromRows(user, "\t", header, row, SongBase.DanceRatingIncrement)[0];
            merge.Tempo = 123;
            Service.UpdateDanceRatingsAndTags(merge,user,new string[]{"SWG"},"Testing:Other","Modern:Style", SongBase.DanceRatingIncrement);

            var changed = Service.AdditiveMerge(user, song.SongId, merge, null);
            Assert.IsTrue(changed);

            const string expected = @".Create=	User=batch	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Tempo=123.0	Length=164	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	DanceRating=FXT+1	Tag+:SFT=Contemporary:Style	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 - Remaster)	Length=153	Album:00=The Pet Sounds Sessions: A 30th Anniversary Collection	Track:00=23	Purchase:00:AS=D:B000T2M00W	Purchase:00:AA=D:B000T2KFKO	Album:01=The Very Best Of The Beach Boys: Sounds Of Summer	Track:01=16	Purchase:01:AS=D:B000TDUV0C	Purchase:01:AA=D:B000TETD9Q	Album:02=Pet Sounds 40th Anniversary Stereo Digital	Track:02=1	Purchase:02:AS=D:B000T060LE	Purchase:02:AA=D:B000T06172	Album:03=50 Big Ones: Greatest Hits	Track:03=24	Purchase:03:AS=D:B009D0IAAA	Purchase:03:AA=D:B009D0Q5PM	Album:04=Pet Sounds	Track:04=15	Purchase:04:AS=D:B000SNW7IM	Purchase:04:AA=D:B000SZZIH2	Tag+=pop:Music|rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 Remaster)	Album:05=Summer Love Songs	Track:05=3	Purchase:05:IS=723863447	Purchase:05:IA=723863135	Tag+=Rock:Music	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 2:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Anna:Other|Gary:Other	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Swing:Dance|Wedding:Other	DanceRating=SWG+2	DanceRating=CSG+1	DanceRating=WCS+1	DanceRating=LHP+1	Tag+:SWG=Modern:Style";
            var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId }));
            Assert.AreEqual(expected, actual);
        }


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
