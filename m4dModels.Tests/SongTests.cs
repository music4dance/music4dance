using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongTests
    {
        //
        // Extend AdminModify to correctly handle tags on dances - we should probably handle DNC* (rather than explicit DNC+2) and then
        //  actually load the tags and do appropriate modification of the tags on the dance ratings.
        //
        //[TestMethod]
        //public void AdminModifyWithDanceTag()
        //{
        //    const string init = @".Create=	User=11101224127	Time=01/05/2018 02:39:33	Title=Pajaro Herido	Artist=Rodolfo Biagi	Length=136	Album:00=Tango Best	Track:00=17	Tag+=Argentine Tango:Dance	DanceRating=ATN+2	Purchase:00:SA=0OysrEZzotITS0fQ22yMne	Purchase:00:SS=7AddIMmrMNrAvfeVLggbdj	DanceRating=TNG+1	.Edit=	User=DWTS	Time=05/13/2015 14:09:23	Tag+=Argentine Tango:Dance|DWTS:Other|Episode 9:Other|Season 20:Other|United States:Other	DanceRating=ATN+2	DanceRating=TNG+1	Tag+:ATN=Allison:Other|Riker:Other	.Edit=	User=batch-a	Time=01/05/2018 02:41:52	Album:01=Rodolfo Biagi Con Sus Cantores: 1939-1947	Track:01=10	Purchase:01:AS=D:B075V5L1WH	Purchase:01:AA=D:B075V711N7	Album:02=The Essence of Tango: Rodolfo Biagi, Vol. 1	Track:02=3	Purchase:02:AS=D:B019EPP092	Purchase:02:AA=D:B019EPQLDQ	Album:03=Tango Classics 076: Cuatro palabras	Track:03=6	Purchase:03:AS=D:B004UPEU52	Purchase:03:AA=D:B004UPE43K	Album:04=A la luz del candil (1941 - 1943)	Track:04=12	Purchase:04:AS=D:B071DNT826	Purchase:04:AA=D:B0713R81XC	Tag+=International:Music|Latin:Music	.Edit=	User=batch-i	Time=01/05/2018 02:41:52	Purchase:04:IS=1231281699	Purchase:04:IA=1231281637	Album:05=Cuatro Palabras	Track:05=6	Purchase:05:IS=429503329	Purchase:05:IA=429503294	Tag+=Latino:Music|World:Music	.Edit=	User=batch-e	Time=01/05/2018 02:41:52	Tempo=107.1	Danceability=0.517	Energy=0.31	Valence=0.692	Tag+=3/4:Tempo";
        //    var song = new Song();
        //    song.Load(init, Stats);
        //    song.AdminModify(
        //        @"{ExcludeUsers:null,Properties:[{Name:'Tag+',Value:'Argentine Tango:Dance',Replace:'Tango Vals:Dance'},{Name:'DanceRating',Value:'ATN+2',Replace:'TGV+2'},{Name:'DanceRating',Value:'MGA+1',Replace:null},{Name:'DanceRating',Value:'TNG+1',Replace:null}]}",
        //        Stats);

        //    Assert.AreEqual(3, song.DanceRatings.Count);
        //    Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "TGV"));
        //    Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "TNG"));
        //    Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "ATN"));
        //    Assert.AreEqual("3/4:Tempo:1|Argentine Tango:Dance:1|DWTS:Other:1|Episode 9:Other:1|International:Music:1|Latin:Music:2|Season 20:Other:1|Tango Vals:Dance:1|United States:Other:1|World:Music:1", song.TagSummary.ToString());
        //}

        //// TODO: Consider if this is a useful test (didn't end up using this code path for what it was testing and it's currently failing
        //[TestMethod]
        //public void DanceRatingMerge()
        //{
        //    var song = new Song();
        //    song.Load(MergeSong, Stats);
        //    Service.Songs.Add(song);

        //    var user = Service.UserManager.FindByName("dwgray");

        //    var header = new List<string> { "Title", "Artist" };
        //    var row = new List<string> { @"Would It Not Be Nice	Beach Boys" };
        //    var merge = Song.CreateFromRows(user, "\t", header, row, SongBase.DanceRatingIncrement)[0];
        //    merge.Tempo = 123;
        //    Service.UpdateDanceRatingsAndTags(merge, user, new[] { "SWG" }, "Testing:Other", "Modern:Style", SongBase.DanceRatingIncrement);

        //    var changed = Service.AdditiveMerge(user, song.SongId, merge, null);
        //    Assert.IsTrue(changed);

        //    const string expected = @".Create=	User=batch	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Tempo=123.0	Length=164	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	DanceRating=FXT+1	Tag+:SFT=Contemporary:Style	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 - Remaster)	Length=153	Album:00=The Pet Sounds Sessions: A 30th Anniversary Collection	Track:00=23	Purchase:00:AS=D:B000T2M00W	Purchase:00:AA=D:B000T2KFKO	Album:01=The Very Best Of The Beach Boys: Sounds Of Summer	Track:01=16	Purchase:01:AS=D:B000TDUV0C	Purchase:01:AA=D:B000TETD9Q	Album:02=Pet Sounds 40th Anniversary Stereo Digital	Track:02=1	Purchase:02:AS=D:B000T060LE	Purchase:02:AA=D:B000T06172	Album:03=50 Big Ones: Greatest Hits	Track:03=24	Purchase:03:AS=D:B009D0IAAA	Purchase:03:AA=D:B009D0Q5PM	Album:04=Pet Sounds	Track:04=15	Purchase:04:AS=D:B000SNW7IM	Purchase:04:AA=D:B000SZZIH2	Tag+=pop:Music|rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 Remaster)	Album:05=Summer Love Songs	Track:05=3	Purchase:05:IS=723863447	Purchase:05:IA=723863135	Tag+=Rock:Music	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 2:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Anna:Other|Gary:Other	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Swing:Dance|Wedding:Other	DanceRating=SWG+2	DanceRating=CSG+1	DanceRating=WCS+1	DanceRating=LHP+1	Tag+:SWG=Modern:Style";
        //    var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId }));
        //    Assert.AreEqual(expected, actual);
        //}


        private static readonly string[] Titles =
        {
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

        private static readonly string[] Normal =
        {
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

        private static readonly string[] Clean =
        {
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

        private static readonly int[] Hashes =
        {
            2068167486,
            -71943789,
            -325959402,
            -1807817725,
            1858788714,
            1865544990,
            381895374,
            -1051298865,
            1796599208,
            1796599208,
            530400665,
            1254262541,
            -1185765647
        };

        private static async Task<DanceMusicCoreService> GetService() =>
            await DanceMusicTester.CreateServiceWithUsers("Song");

        private static async Task<DanceStatsInstance> GetStats() => (await GetService()).DanceStats;

        [TestMethod]
        public void NormalForm()
        {
            for (var i = 0; i < Titles.Length; i++)
            {
                var t = Titles[i];

                var n = Song.CreateNormalForm(t);
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

                var hash = Song.CreateTitleHash(t);
                Trace.WriteLine(hash);
                //Assert.AreEqual(Hashes[i], hash);
            }
        }

        [TestMethod]
        public void NormalString()
        {
            for (var i = 0; i < Titles.Length; i++)
            {
                var t = Titles[i];

                var n = Song.CreateNormalForm(t);
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

                var n = Song.CleanString(t);
                Assert.AreEqual(Clean[i], n);
                //Trace.WriteLine(string.Format("{0}",Song.CreateTitleHash(t)));
            }
        }

        [TestMethod]
        public void AlbumFields()
        {
            Assert.IsTrue(Song.IsAlbumField(Song.AlbumField));
            Assert.IsFalse(Song.IsAlbumField(Song.TitleField));

            Assert.IsTrue(Song.IsAlbumField(Song.PublisherField + ":0"));
            Assert.IsTrue(Song.IsAlbumField(Song.TrackField + ":45:A"));

            Assert.IsFalse(Song.IsAlbumField(null));
            Assert.IsFalse(Song.IsAlbumField("#"));
        }

        [TestMethod]
        public async Task Ratings()
        {
            var song = new Song();
            await song.Load(@"User=batch	Title=Test	Artist=Me	Tempo=30.0", await GetService());

            song.UpdateDanceRatings(new[] { "RMB", "CHA" }, 5);
            song.UpdateDanceRatings(new[] { "FXT" }, 7);

            // TESTTODO: Figure out if we need to do the user part of this
            //Create an test an initial small list of dance ratings
            //const string user = "dwgray";
            Assert.IsTrue(song.DanceRatings.Count == 3);

            // Now mix it up a bit

            song.UpdateDanceRatings(new[] { "RMB", "FXT" }, 3);
            Assert.IsTrue(song.DanceRatings.Count == 3);
            var drT = song.FindRating("RMB");
            Assert.IsTrue(drT.Weight == 8);

            song.UpdateDanceRatings(new[] { "CHA", "FXT" }, -5);
            Assert.IsTrue(song.DanceRatings.Count == 2);
            drT = song.FindRating("FXT");
            Assert.IsTrue(drT.Weight == 5);
        }

        [TestMethod]
        public async Task UpdateRatings()
        {
            var service = await GetService();
            var stats = await GetStats();
            var user1 = await service.FindUser("batch");
            var user2 = await service.FindUser("dwgray");

            var song = new Song();
            await song.Load(
                @"User=batch	Title=Test	Artist=Me	Tempo=30.0	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5",
                service);

            var drT = song.FindRating("SFT");
            Assert.AreEqual(5, drT.Weight);

            Assert.AreEqual(5, song.UserDanceRating(user1.UserName, "SFT"));
            Assert.AreEqual(0, song.UserDanceRating(user1.UserName, "SWG"));
            Assert.AreEqual(0, song.UserDanceRating(user2.UserName, "SFT"));

            song.EditDanceLike(user1, true, "SFT", stats);
            Assert.AreEqual(5, song.UserDanceRating(user1.UserName, "SFT"));

            song.EditDanceLike(user2, true, "SFT", stats);
            Assert.AreEqual(5, song.UserDanceRating(user1.UserName, "SFT"));
            Assert.AreEqual(Song.DanceRatingIncrement, song.UserDanceRating(user2.UserName, "SFT"));
            drT = song.FindRating("SFT");
            Assert.AreEqual(Song.DanceRatingIncrement + 5, drT.Weight);
            // TESTTODO: ???
            //var ut = song.UserTags(user2, Stats);
            //Assert.AreEqual("Slow Foxtrot:Dance", ut.ToString());

            song.EditDanceLike(user1, null, "SFT", stats);
            Assert.AreEqual(0, song.UserDanceRating(user1.UserName, "SFT"));
            Assert.AreEqual(Song.DanceRatingIncrement, song.UserDanceRating(user2.UserName, "SFT"));
            //ut = song.UserTags(user1, Stats);
            //Assert.IsTrue(ut.IsEmpty);

            song.EditDanceLike(user2, null, "SFT", stats);
            Assert.AreEqual(0, song.UserDanceRating(user2.UserName, "SFT"));
            drT = song.FindRating("SFT");
            Assert.IsNull(drT);

            song.EditDanceLike(user2, false, "SWG", stats);
            Assert.AreEqual(Song.DanceRatingDecrement, song.UserDanceRating(user2.UserName, "SWG"));
            drT = song.FindRating("SWG");
            Assert.IsNull(drT);
        }

        //private const string ModeratorOriginal = @".Create=	User=DWTS	Time=6/23/2014 2:03:23 PM	Title=Temperature	Artist=Sean Paul	Tempo=126.0	DanceRating=PLK+5	User=DWTS	Time=6/23/2014 2:05:17 PM	DanceRating=PDL+6	User=batch	Time=6/24/2014 8:33:08 AM	Length=217	Album:00=The Trinity	Track:00=11	Purchase:00:XS=music.919F5000-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch	Time=6/24/2014 9:34:32 AM	Length=219	Purchase:00:IS=80429794	Purchase:00:IA=80429921	User=batch	Time=6/24/2014 10:02:21 AM	Length=218	Album:00=The Trinity (Domestic Album Version)	Purchase:00:AS=D:B0011Z95J0	Purchase:00:AA=D:B0011Z1BLK	User=batch	Time=10/9/2014 11:00:39 AM	DanceRating=LTN+3	User=DWTS	Time=11/20/2014 11:30:37 AM	Tag+=Paso Doble:Dance|Polka:Dance	User=batch	Time=11/20/2014 11:30:37 AM	Tag+=Pop:Music	.Edit=	User=batch-a	Time=12/10/2014 8:38:46 PM	Length=219	Album:01=Riddim Driven - Applause	Track:01=2	Purchase:01:AS=D:B0019C3ZB4	Purchase:01:AA=D:B0019C27VI	Tag+=International:Music|pop:Music	.Edit=	User=batch-i	Time=12/10/2014 8:38:49 PM	Length=216	Album:01=Riddim Driven: Applause	Purchase:01:IS=73599408	Purchase:01:IA=73599818	Album:02=The Trinity	Track:02=11	Purchase:02:IS=80429794	Purchase:02:IA=80429921	Album:03=Reggae Gold 2012	Track:03=3	Purchase:03:IS=534566940	Purchase:03:IA=534566816	Album:04=Reggae Gold 2006	Track:04=1	Purchase:04:IS=151476210	Purchase:04:IA=151476209	Album:05=Temperature Rising	Track:05=11	Purchase:05:IS=937317776	Purchase:05:IA=937317713	Tag+=Pop:Music|Reggae:Music|World:Music	.Edit=	User=batch-x	Time=12/10/2014 8:38:51 PM	Length=219	Album:01=Riddim Driven - Applause	Purchase:01:XS=music.A1F9FA00-0100-11DB-89CA-0019B92A3933	Purchase:02:XS=music.919F5000-0100-11DB-89CA-0019B92A3933	Track:03=20	Purchase:03:XS=music.54BD3E07-0100-11DB-89CA-0019B92A3933	Purchase:04:XS=music.7BDF9407-0100-11DB-89CA-0019B92A3933	Tag+=Pop:Music|Reggae / Dancehall:Music	.Edit=	User=batch-s	Time=1/6/2015 5:00:16 PM	Length=216	Album:01=Riddim Driven: Applause	Purchase:01:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO]	Purchase:01:SA=0Oy4Q9QsoRFxdzcMVwF0Rm	Purchase:02:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO]	Purchase:02:SA=5dllg7LmHBB2pOSzr9aOg0	Track:03=2003	Purchase:03:SS=60QKgPDQYq8pDtqM8ON1PZ[CA,US]	Purchase:03:SA=6TSAZ7AD18RpnDCHweTmrX	Purchase:04:SS=7e4QLZDINRr1CkZWm86C7C[US]	Purchase:04:SA=0AvG4u0F78RSUByBljNuX3	Album:06=Temperature	Track:06=1	Purchase:06:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO,FR]	Purchase:06:SA=5KtEDltVhZyUxVqi81J84n	Album:07=NOW!11	Track:07=3	Purchase:07:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO,FR]	Purchase:07:SA=0OEBaT8sRiHUbUWHMHe2Qi	.Edit=	User=batch	Time=01/11/2016 18:42:01	Album:00=The Trinity	Purchase:00:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO]	Purchase:00:SA=5dllg7LmHBB2pOSzr9aOg0	Album:02=	Track:02=	.FailedLookup=-:0	.Edit=	User=ohdwg	Time=02/06/2016 01:39:06	Tag+=Polka:Dance	DanceRating=PLK+2	.Edit=	User=ohdwg	Time=02/06/2016 01:39:08	Tag+=!Polka:Dance	Tag-=Polka:Dance	DanceRating=PLK-3	.Edit=	User=batch-s	Time=02/08/2016 19:25:07	Sample=https://p.scdn.co/mp3-preview/4fb6d452f24697ba91658d1c0d892e55a27767b0	.Edit=	User=batch-e	Time=02/09/2016 18:04:53	Tempo=125.0	Danceability=0.9511878	Energy=0.5966625	Valence=0.829769	Tag+=4/4:Tempo	.Edit=	User=dwgray	Time=02/19/2016 10:22:39	DanceRating=PBD+2	DanceRating=PLK+2	DanceRating=LTN-1	Tag+=!Latin:Dance|Peabody:Dance|Polka:Dance	Tag+:PLK=Contemporary:Style|First Dance:Other|Wedding:Other	Tag+:PBD=Unconditional:Other|Unconventional:Style";
        //private const string ModeratorExpected = @".Create=	User=DWTS	Time=00/00/0000 0:00:00 PM	Title=Temperature	Artist=Sean Paul	Tempo=126.0	DanceRating=PLK+5	User=DWTS	Time=00/00/0000 0:00:00 PM	DanceRating=PDL+6	User=batch	Time=00/00/0000 0:00:00 PM	Length=217	Album:00=The Trinity	Track:00=11	Purchase:00:XS=music.919F5000-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch	Time=00/00/0000 0:00:00 PM	Length=219	Purchase:00:IS=80429794	Purchase:00:IA=80429921	User=batch	Time=00/00/0000 0:00:00 PM	Length=218	Album:00=The Trinity (Domestic Album Version)	Purchase:00:AS=D:B0011Z95J0	Purchase:00:AA=D:B0011Z1BLK	User=batch	Time=00/00/0000 0:00:00 PM	DanceRating=LTN+3	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Paso Doble:Dance|Polka:Dance	User=batch	Time=00/00/0000 0:00:00 PM	Tag+=Pop:Music	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Length=219	Album:01=Riddim Driven - Applause	Track:01=2	Purchase:01:AS=D:B0019C3ZB4	Purchase:01:AA=D:B0019C27VI	Tag+=International:Music|pop:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Length=216	Album:01=Riddim Driven: Applause	Purchase:01:IS=73599408	Purchase:01:IA=73599818	Album:02=The Trinity	Track:02=11	Purchase:02:IS=80429794	Purchase:02:IA=80429921	Album:03=Reggae Gold 2012	Track:03=3	Purchase:03:IS=534566940	Purchase:03:IA=534566816	Album:04=Reggae Gold 2006	Track:04=1	Purchase:04:IS=151476210	Purchase:04:IA=151476209	Album:05=Temperature Rising	Track:05=11	Purchase:05:IS=937317776	Purchase:05:IA=937317713	Tag+=Pop:Music|Reggae:Music|World:Music	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Length=219	Album:01=Riddim Driven - Applause	Purchase:01:XS=music.A1F9FA00-0100-11DB-89CA-0019B92A3933	Purchase:02:XS=music.919F5000-0100-11DB-89CA-0019B92A3933	Track:03=20	Purchase:03:XS=music.54BD3E07-0100-11DB-89CA-0019B92A3933	Purchase:04:XS=music.7BDF9407-0100-11DB-89CA-0019B92A3933	Tag+=Pop:Music|Reggae / Dancehall:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Length=216	Album:01=Riddim Driven: Applause	Purchase:01:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO]	Purchase:01:SA=0Oy4Q9QsoRFxdzcMVwF0Rm	Purchase:02:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO]	Purchase:02:SA=5dllg7LmHBB2pOSzr9aOg0	Track:03=2003	Purchase:03:SS=60QKgPDQYq8pDtqM8ON1PZ[CA,US]	Purchase:03:SA=6TSAZ7AD18RpnDCHweTmrX	Purchase:04:SS=7e4QLZDINRr1CkZWm86C7C[US]	Purchase:04:SA=0AvG4u0F78RSUByBljNuX3	Album:06=Temperature	Track:06=1	Purchase:06:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO,FR]	Purchase:06:SA=5KtEDltVhZyUxVqi81J84n	Album:07=NOW!11	Track:07=3	Purchase:07:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO,FR]	Purchase:07:SA=0OEBaT8sRiHUbUWHMHe2Qi	.Edit=	User=batch	Time=00/00/0000 0:00:00 PM	Album:00=The Trinity	Purchase:00:SS=0k2GOhqsrxDTAbFFSdNJjT[0-DO]	Purchase:00:SA=5dllg7LmHBB2pOSzr9aOg0	Album:02=	Track:02=	.FailedLookup=-:0	.Edit=	User=ohdwg	Time=00/00/0000 0:00:00 PM	Tag+=Polka:Dance	DanceRating=PLK+2	.Edit=	User=ohdwg	Time=00/00/0000 0:00:00 PM	Tag+=!Polka:Dance	Tag-=Polka:Dance	DanceRating=PLK-3	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Sample=https://p.scdn.co/mp3-preview/4fb6d452f24697ba91658d1c0d892e55a27767b0	.Edit=	User=batch-e	Time=00/00/0000 0:00:00 PM	Tempo=125.0	Danceability=0.9511878	Energy=0.5966625	Valence=0.829769	Tag+=4/4:Tempo	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	DanceRating=PBD+2	DanceRating=PLK+2	DanceRating=LTN-1	Tag+=!Latin:Dance|Peabody:Dance|Polka:Dance	Tag+:PLK=Contemporary:Style|First Dance:Other|Wedding:Other	Tag+:PBD=Unconditional:Other|Unconventional:Style	.Edit=	User=Charlie	Time=00/00/0000 0:00:00 PM	DanceRating=PBD+2	DanceRating=LTN-1	Tag+=!Latin:Dance|Peabody:Dance	Tag+:PBD=Unconditional:Other|Unconventional:Style	UserProxy=DWTS	Tag-=Paso Doble:Dance|Polka:Dance	UserProxy=ohdwg	Tag-=!Polka:Dance	UserProxy=dwgray	Tag-=Polka:Dance	UserProxy=Charlie	DanceRating=PDL-6	DanceRating=PLK-6";
        //private static void ModeratorVerifyOriginal(Song song, bool verifyGlobal = true)
        //{
        //    var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
        //    Trace.WriteLine(actual);
        //    Assert.AreEqual(DanceMusicTester.ReplaceTime(ModeratorOriginal), actual);

        //    if (verifyGlobal)
        //    {
        //        Assert.AreEqual(1, Service.TagGroups.Find("!Latin:Dance").Count);
        //        Assert.AreEqual(2, Service.TagGroups.Find("Polka:Dance").Count);
        //        Assert.AreEqual(1, Service.TagGroups.Find("!Polka:Dance").Count);
        //        Assert.AreEqual(1, Service.TagGroups.Find("Unconventional:Style").Count);
        //    }

        //    Assert.IsNotNull(song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "PLK"));
        //    Assert.IsNotNull(song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "PDL"));
        //}

        //private static void ModeratorVerifyDeleted(Song song)
        //{
        //    var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
        //    Trace.WriteLine(actual);
        //    Assert.AreEqual(ModeratorExpected, actual);

        //    Trace.WriteLine(Service.TagGroups.Find("!Latin:Dance").Count);
        //    Trace.WriteLine(Service.TagGroups.Find("Polka:Dance").Count);
        //    Trace.WriteLine(Service.TagGroups.Find("!Polka:Dance").Count);
        //    Trace.WriteLine(Service.TagGroups.Find("Unconventional:Style").Count);

        //    Assert.AreEqual(2, Service.TagGroups.Find("!Latin:Dance").Count);
        //    Assert.AreEqual(0, Service.TagGroups.Find("Polka:Dance").Count);
        //    Assert.AreEqual(0, Service.TagGroups.Find("!Polka:Dance").Count);
        //    Assert.AreEqual(2, Service.TagGroups.Find("Unconventional:Style").Count);

        //    Assert.IsNull(song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "PLK"));
        //    Assert.IsNull(song.DanceRatings.FirstOrDefault(dr => dr.DanceId == "PDL"));
        //}

        //private static void CleanTagTypes()
        //{
        //    var old = Service.TagGroups.ToList();
        //    foreach (var tt in old)
        //    {
        //        Service.TagGroups.Remove(tt);
        //    }
        //}

        //// TESTTODO: This requires some refactoring but should still be testable
        //[TestMethod]
        //public void ModeratorRatings()
        //{
        //    CleanTagTypes();

        //    var song = new Song();

        //    song.Load(ModeratorOriginal, Stats);

        //    ModeratorVerifyOriginal(song);

        //    var user = Service.FindUser("Charlie");
        //    var tags = new List<UserTag>
        //    {
        //        new UserTag { Id="", Tags=new TagList("null:Like|!Latin:Dance|Peabody:Dance|^Polka:Dance|^Paso Doble:Dance") },
        //        new UserTag { Id="PBD", Tags=new TagList("Unconditional:Other|Unconventional:Style") },
        //    };

        //    song.EditTags("Charlie", tags, Stats);

        //    ModeratorVerifyDeleted(song);

        //    // Test userUndo
        //    Service.UndoUserChanges(user, song.SongId);
        //    ModeratorVerifyOriginal(song, false);
        //}

        //[TestMethod]
        //public void ModeratorRatingLoad()
        //{
        //    // Test reload based on moderator settings
        //    CleanTagTypes();
        //    // TODO: Do we have a general bug where if a dance rating has been fully deleted, BaseTagsFrom Properties fails?
        //    var song = new Song();
        //    song.Load(ModeratorExpected, Stats);
        //    ModeratorVerifyDeleted(song);
        //}

        //private const string MergeSong =
        //    @".Create=	User=batch	Time=04/15/2015 21:15:47	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Tempo=123.0	Length=164	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	DanceRating=FXT+1	Tag+:SFT=Contemporary:Style	.Edit=	User=batch-a	Time=4/15/2015 9:27:05 PM	Title=Wouldn't It Be Nice (2000 - Remaster)	Length=153	Album:00=The Pet Sounds Sessions: A 30th Anniversary Collection	Track:00=23	Purchase:00:AS=D:B000T2M00W	Purchase:00:AA=D:B000T2KFKO	Album:01=The Very Best Of The Beach Boys: Sounds Of Summer	Track:01=16	Purchase:01:AS=D:B000TDUV0C	Purchase:01:AA=D:B000TETD9Q	Album:02=Pet Sounds 40th Anniversary Stereo Digital	Track:02=1	Purchase:02:AS=D:B000T060LE	Purchase:02:AA=D:B000T06172	Album:03=50 Big Ones: Greatest Hits	Track:03=24	Purchase:03:AS=D:B009D0IAAA	Purchase:03:AA=D:B009D0Q5PM	Album:04=Pet Sounds	Track:04=15	Purchase:04:AS=D:B000SNW7IM	Purchase:04:AA=D:B000SZZIH2	Tag+=pop:Music|rock:Music	.Edit=	User=batch-i	Time=4/15/2015 9:27:05 PM	Title=Wouldn't It Be Nice (2000 Remaster)	Album:05=Summer Love Songs	Track:05=3	Purchase:05:IS=723863447	Purchase:05:IA=723863135	Tag+=Rock:Music	.Edit=	User=batch-x	Time=4/15/2015 9:27:07 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=DWTS	Time=09/23/2015 16:00:15	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 2:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Anna:Other|Gary:Other";
        //[TestMethod]
        //public void AdditiveMerge()
        //{
        //    var songs = Service.Songs;
        //    var song = new Song();
        //    song.Load(MergeSong, Stats);
        //    songs.Add(song);

        //    var user = Service.UserManager.FindByName("dwgray");
        //    var header = new List<string> { "Title", "Artist", "DanceRating", "DanceTags:Style", "SongTags:Other" };
        //    var row = new List<string> { @"Would It Not Be Nice	Beach Boys	Swing	Modern	Wedding" };
        //    var merge = Song.CreateFromRows(user, "\t", header, row, Stats, Song.DanceRatingIncrement)[0];
        //    merge.Tempo = 123;
        //    merge.InferDances(user);

        //    var changed = Service.AdditiveMerge(user, song.SongId, merge, null);
        //    Assert.IsTrue(changed);

        //    const string expected = @".Create=	User=batch	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Tempo=123.0	Length=164	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	DanceRating=FXT+1	Tag+:SFT=Contemporary:Style	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 - Remaster)	Length=153	Album:00=The Pet Sounds Sessions: A 30th Anniversary Collection	Track:00=23	Purchase:00:AS=D:B000T2M00W	Purchase:00:AA=D:B000T2KFKO	Album:01=The Very Best Of The Beach Boys: Sounds Of Summer	Track:01=16	Purchase:01:AS=D:B000TDUV0C	Purchase:01:AA=D:B000TETD9Q	Album:02=Pet Sounds 40th Anniversary Stereo Digital	Track:02=1	Purchase:02:AS=D:B000T060LE	Purchase:02:AA=D:B000T06172	Album:03=50 Big Ones: Greatest Hits	Track:03=24	Purchase:03:AS=D:B009D0IAAA	Purchase:03:AA=D:B009D0Q5PM	Album:04=Pet Sounds	Track:04=15	Purchase:04:AS=D:B000SNW7IM	Purchase:04:AA=D:B000SZZIH2	Tag+=pop:Music|rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice (2000 Remaster)	Album:05=Summer Love Songs	Track:05=3	Purchase:05:IS=723863447	Purchase:05:IA=723863135	Tag+=Rock:Music	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 2:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Anna:Other|Gary:Other	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Swing:Dance|Wedding:Other	DanceRating=SWG+2	DanceRating=CSG+1	DanceRating=WCS+1	DanceRating=LHP+1	Tag+:SWG=Modern:Style";
        //    var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
        //    Assert.AreEqual(expected, actual);
        //}

        //private static readonly string[] AlbumSongs = {
        //        @"User=dwgray	OwnerHash=-1863299954	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Artist=The Doors	Tempo=119.8	Length=131	Album:00=Best Of The Doors [Disc 1]	Track:00=4	Publisher:00=WEA	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 7:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Tamar:Other|Val:Other	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Length=130	Album:01=Strange Days	Track:01=7	Purchase:01:AS=D:B0018AO430	Purchase:01:AA=D:B0018ARNR4	Album:02=The Very Best Of [w/bonus tracks]	Track:02=10	Purchase:02:AS=D:B001232CDW	Purchase:02:AA=D:B00122X4JE	Album:03=When You're Strange (Songs From The Motion Picture)	Track:03=15	Purchase:03:AS=D:B003ELZ52K	Purchase:03:AA=D:B003EM54IE	Album:04=The Complete Studio Albums	Track:04=7	Purchase:04:AS=D:B00H7L85VQ	Purchase:04:AA=D:B00H7L7WOM	Album:05=The Future Starts Here: The Essential Doors Hits	Track:05=5	Purchase:05:AS=D:B0012QLOVU	Purchase:05:AA=D:B0012QK80S	Tag+=classic-rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=People Are Strange (Live At the Matrix)	Length=135	Purchase:01:IS=640074407	Purchase:01:IA=640074114	Purchase:05:IS=641333125	Purchase:05:IA=641332994	Album:06=When You're Strange (Songs from the Motion Picture) [Deluxe Version]	Track:06=15	Purchase:06:IS=363701078	Purchase:06:IA=363700945	Album:07=The Very Best of the Doors	Track:07=10	Purchase:07:IS=640047588	Purchase:07:IA=640047463	Album:08=Live At the Matrix	Track:08=17	Purchase:08:IS=639676770	Purchase:08:IA=639676554	Tag+=Rock:Music|Soundtrack:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Title=People Are Strange [Live At The Matrix]	Album:09=Strange Nights Of Stone	Track:09=2017	Purchase:09:SS=1VbzTlxsJdW1HFU8rdDhE4	Purchase:09:SA=7bs3oHD0CaOQxUSdjr3osB	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Length=130	Purchase:01:XS=music.89E16C00-0100-11DB-89CA-0019B92A3933	Purchase:01:MS=T  1532957	Track:04=18	Purchase:04:XS=music.3AB30408-0100-11DB-89CA-0019B92A3933	Track:09=33	Purchase:09:XS=music.EB2FB307-0100-11DB-89CA-0019B92A3933	Purchase:09:MS=T 30439516	Album:10=The Future Starts Here: The Essential Doors Hits (New Stereo Mix)	Track:10=5	Purchase:10:XS=music.AD67C100-0100-11DB-89CA-0019B92A3933	Purchase:10:MS=T 13610812	Album:11=When You're Strange	Track:11=15	Purchase:11:XS=music.7A8F1306-0100-11DB-89CA-0019B92A3933	Purchase:11:MS=T 28755028	Album:12=The Very Best Of The Doors (Bonus Tracks)	Track:12=10	Purchase:12:XS=music.8F06B000-0100-11DB-89CA-0019B92A3933	Tag+=Rock:Music	.FailedLookup=-:0",
        //        @"User=dwgray	Time=00/00/0000 0:00:00 PM	Title=At Last	Artist=Nat ""King"" Cole	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Artist=Nat 'King' Cole	Length=178	Album:00=Voices Of Change, Then And Now	Track:00=3	Purchase:00:AS=D:B001RJZXIC	Purchase:00:AA=D:B001RK98L4	Album:01=Things We Do At Night	Track:01=26	Purchase:01:AS=D:B00L9JKMSQ	Purchase:01:AA=D:B00L9JK600	Album:02=Songs From The Heart	Track:02=18	Purchase:02:AS=D:B001ANHP8Q	Purchase:02:AA=D:B001ANHOOG	Album:03=Love Is The Thing (Digital)	Track:03=7	Purchase:03:AS=D:B001AL1ZTS	Purchase:03:AA=D:B001AL2010	Album:04=The History of Jazz Vol. 8	Track:04=18	Purchase:04:AS=D:B00TJ6JF3Y	Purchase:04:AA=D:B00TJ6H25C	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Artist=Nat ""King"" Cole	Length=180	Album:05=Voices of Change, Then and Now - EP	Track:05=3	Purchase:05:IS=716435737	Purchase:05:IA=716435712	Album:06=Songbook Series: Songs from the Heart	Track:06=18	Purchase:06:IS=724732281	Purchase:06:IA=724731774	Album:07=Love Is the Thing (And More)	Track:07=7	Purchase:07:IS=724954084	Purchase:07:IA=724951463	Tag+=Pop:Music|Vocal Pop:Music|Vocal:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Artist=Nat King Cole	Album:08=Love Is The Thing	Track:08=7	Purchase:08:SS=6pLzRKiNpmNz53Eism3vyr	Purchase:08:SA=0M74fKKEBEFUSmiGbjIkps	Album:09=The Unforgettable Nat King Cole	Track:09=2018	Purchase:09:SS=5VknVSEGCZaCBa3IfkTzUw	Purchase:09:SA=2GbhcTRu7BCPkss4E2YMsh	Album:10=30 Greatest Love Songs	Track:10=23	Purchase:10:SS=2iAWs4wdnilZl6pCAaTBeV	Purchase:10:SA=3Sh3Kg7mBJ0vT9yHnTUqOb	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Length=177	Purchase:00:XS=music.4E9B4A06-0100-11DB-89CA-0019B92A3933	Purchase:01:XS=music.10C76608-0100-11DB-89CA-0019B92A3933	Purchase:04:XS=music.23B6CD08-0100-11DB-89CA-0019B92A3933	Album:06=Songbook Series: Songs From The Heart	Purchase:06:XS=music.6E272B06-0100-11DB-89CA-0019B92A3933	Purchase:08:XS=music.7E584106-0100-11DB-89CA-0019B92A3933	Track:09=45	Purchase:09:XS=music.295F9D01-0100-11DB-89CA-0019B92A3933	Album:11=There Is No You	Track:11=10	Purchase:11:XS=music.E1BFFD06-0100-11DB-89CA-0019B92A3933	Tag+=Jazz:Music|More:Music|Pop:Music	.FailedLookup=-:0	.Edit=	User=batch	Time=00/00/0000 0:00:00 PM	Tempo=60.0	Tag+=Castle Foxtrot:Dance|Weak:Tempo	DanceRating=CFT+3	DanceRating=FXT+1	DanceRating=CFT+1"
        //    };
        //private static readonly string[] AlbumExpected = {
        //        @"User=dwgray	OwnerHash=-1863299954	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Artist=The Doors	Tempo=119.8	Length=131	Album:00=Best Of The Doors [Disc 1]	Track:00=4	Publisher:00=WEA	.Edit=	User=DWTS	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	Tag+=DWTS:Other|Episode 7:Other|Season 21:Other|United States:Other	DanceRating=FXT+3	Tag+:FXT=Tamar:Other|Val:Other	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Length=130	Album:01=Strange Days	Track:01=7	Purchase:01:AS=D:B0018AO430	Purchase:01:AA=D:B0018ARNR4	Album:02=The Very Best Of [w/bonus tracks]	Track:02=10	Purchase:02:AS=D:B001232CDW	Purchase:02:AA=D:B00122X4JE	Album:03=When You're Strange (Songs From The Motion Picture)	Track:03=15	Purchase:03:AS=D:B003ELZ52K	Purchase:03:AA=D:B003EM54IE	Album:04=The Complete Studio Albums	Track:04=7	Purchase:04:AS=D:B00H7L85VQ	Purchase:04:AA=D:B00H7L7WOM	Album:05=The Future Starts Here: The Essential Doors Hits	Track:05=5	Purchase:05:AS=D:B0012QLOVU	Purchase:05:AA=D:B0012QK80S	Tag+=classic-rock:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Title=People Are Strange (Live At the Matrix)	Length=135	Purchase:01:IS=640074407	Purchase:01:IA=640074114	Purchase:05:IS=641333125	Purchase:05:IA=641332994	Album:06=When You're Strange (Songs from the Motion Picture) [Deluxe Version]	Track:06=15	Purchase:06:IS=363701078	Purchase:06:IA=363700945	Album:07=The Very Best of the Doors	Track:07=10	Purchase:07:IS=640047588	Purchase:07:IA=640047463	Album:08=Live At the Matrix	Track:08=17	Purchase:08:IS=639676770	Purchase:08:IA=639676554	Tag+=Rock:Music|Soundtrack:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Title=People Are Strange [Live At The Matrix]	Album:09=Strange Nights Of Stone	Track:09=2017	Purchase:09:SS=1VbzTlxsJdW1HFU8rdDhE4	Purchase:09:SA=7bs3oHD0CaOQxUSdjr3osB	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Title=People Are Strange	Length=130	Purchase:01:XS=music.89E16C00-0100-11DB-89CA-0019B92A3933	Purchase:01:MS=T  1532957	Track:04=18	Purchase:04:XS=music.3AB30408-0100-11DB-89CA-0019B92A3933	Track:09=33	Purchase:09:XS=music.EB2FB307-0100-11DB-89CA-0019B92A3933	Purchase:09:MS=T 30439516	Album:10=The Future Starts Here: The Essential Doors Hits (New Stereo Mix)	Track:10=5	Purchase:10:XS=music.AD67C100-0100-11DB-89CA-0019B92A3933	Purchase:10:MS=T 13610812	Album:11=When You're Strange	Track:11=15	Purchase:11:XS=music.7A8F1306-0100-11DB-89CA-0019B92A3933	Purchase:11:MS=T 28755028	Album:12=The Very Best Of The Doors (Bonus Tracks)	Track:12=10	Purchase:12:XS=music.8F06B000-0100-11DB-89CA-0019B92A3933	Tag+=Rock:Music	.FailedLookup=-:0	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Album:02=The Very Best of the Doors	Purchase:02:IS=640047588	Purchase:02:IA=640047463	Purchase:02:XS=music.8F06B000-0100-11DB-89CA-0019B92A3933	Album:03=When You're Strange	Purchase:03:IS=363701078	Purchase:03:IA=363700945	Purchase:03:XS=music.7A8F1306-0100-11DB-89CA-0019B92A3933	Purchase:03:MS=T 28755028	Purchase:05:XS=music.AD67C100-0100-11DB-89CA-0019B92A3933	Purchase:05:MS=T 13610812	Album:06=	Track:06=	Album:07=	Track:07=	Album:10=	Track:10=	Album:11=	Track:11=	Album:12=	Track:12=",
        //        @"User=dwgray	Time=00/00/0000 0:00:00 PM	Title=At Last	Artist=Nat ""King"" Cole	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Edit=	User=batch-a	Time=00/00/0000 0:00:00 PM	Artist=Nat 'King' Cole	Length=178	Album:00=Voices Of Change, Then And Now	Track:00=3	Purchase:00:AS=D:B001RJZXIC	Purchase:00:AA=D:B001RK98L4	Album:01=Things We Do At Night	Track:01=26	Purchase:01:AS=D:B00L9JKMSQ	Purchase:01:AA=D:B00L9JK600	Album:02=Songs From The Heart	Track:02=18	Purchase:02:AS=D:B001ANHP8Q	Purchase:02:AA=D:B001ANHOOG	Album:03=Love Is The Thing (Digital)	Track:03=7	Purchase:03:AS=D:B001AL1ZTS	Purchase:03:AA=D:B001AL2010	Album:04=The History of Jazz Vol. 8	Track:04=18	Purchase:04:AS=D:B00TJ6JF3Y	Purchase:04:AA=D:B00TJ6H25C	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=00/00/0000 0:00:00 PM	Artist=Nat ""King"" Cole	Length=180	Album:05=Voices of Change, Then and Now - EP	Track:05=3	Purchase:05:IS=716435737	Purchase:05:IA=716435712	Album:06=Songbook Series: Songs from the Heart	Track:06=18	Purchase:06:IS=724732281	Purchase:06:IA=724731774	Album:07=Love Is the Thing (And More)	Track:07=7	Purchase:07:IS=724954084	Purchase:07:IA=724951463	Tag+=Pop:Music|Vocal Pop:Music|Vocal:Music	.Edit=	User=batch-s	Time=00/00/0000 0:00:00 PM	Artist=Nat King Cole	Album:08=Love Is The Thing	Track:08=7	Purchase:08:SS=6pLzRKiNpmNz53Eism3vyr	Purchase:08:SA=0M74fKKEBEFUSmiGbjIkps	Album:09=The Unforgettable Nat King Cole	Track:09=2018	Purchase:09:SS=5VknVSEGCZaCBa3IfkTzUw	Purchase:09:SA=2GbhcTRu7BCPkss4E2YMsh	Album:10=30 Greatest Love Songs	Track:10=23	Purchase:10:SS=2iAWs4wdnilZl6pCAaTBeV	Purchase:10:SA=3Sh3Kg7mBJ0vT9yHnTUqOb	.Edit=	User=batch-x	Time=00/00/0000 0:00:00 PM	Length=177	Purchase:00:XS=music.4E9B4A06-0100-11DB-89CA-0019B92A3933	Purchase:01:XS=music.10C76608-0100-11DB-89CA-0019B92A3933	Purchase:04:XS=music.23B6CD08-0100-11DB-89CA-0019B92A3933	Album:06=Songbook Series: Songs From The Heart	Purchase:06:XS=music.6E272B06-0100-11DB-89CA-0019B92A3933	Purchase:08:XS=music.7E584106-0100-11DB-89CA-0019B92A3933	Track:09=45	Purchase:09:XS=music.295F9D01-0100-11DB-89CA-0019B92A3933	Album:11=There Is No You	Track:11=10	Purchase:11:XS=music.E1BFFD06-0100-11DB-89CA-0019B92A3933	Tag+=Jazz:Music|More:Music|Pop:Music	.FailedLookup=-:0	.Edit=	User=batch	Time=00/00/0000 0:00:00 PM	Tempo=60.0	Tag+=Castle Foxtrot:Dance|Weak:Tempo	DanceRating=CFT+3	DanceRating=FXT+1	DanceRating=CFT+1	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Purchase:00:IS=716435737	Purchase:00:IA=716435712	Album:03=Love Is The Thing	Purchase:03:IS=724954084	Purchase:03:IA=724951463	Purchase:03:SS=6pLzRKiNpmNz53Eism3vyr	Purchase:03:SA=0M74fKKEBEFUSmiGbjIkps	Purchase:03:XS=music.7E584106-0100-11DB-89CA-0019B92A3933	Album:05=	Track:05=	Album:07=	Track:07=	Album:08=	Track:08=	DanceRating=FXT+1	DanceRating=CFT+1"
        //};

        //[TestMethod]
        //public void AlbumMerge()
        //{
        //    for (var i = 0; i < AlbumSongs.Length; i++)
        //    {
        //        var song = new Song();
        //        song.Load(AlbumSongs[i], Stats);
        //        Service.Songs.Add(song);

        //        //Trace.WriteLine(DanceMusicTester.ReplaceTime(song.Serialize(new[] { SongBase.NoSongId })));

        //        var user = Service.UserManager.FindByName("dwgray");
        //        Service.CleanupAlbums(user, song);

        //        var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
        //        //Trace.WriteLine(actual);
        //        Assert.AreEqual(AlbumExpected[i], actual);
        //    }
        //}

        [TestMethod]
        public async Task DeletedDance()
        {
            var song = new Song();
            await song.Load(
                @".Create=	User=dgsnure	Time=03/12/2021 12:29:51	Title=Otro en Su Cama	Artist=Jhonny Evidence	Length=213	DanceRating=LTN+1	.Edit=	User=batch-i	Time=03/12/2021 12:29:53	Tag+=Música Tropical:Music	.Edit=	User=batch-s	Time=03/12/2021 12:29:53	Tag+=Bachata Dominicana:Music	.Edit=	User=batch-e	Time=03/12/2021 12:29:53	Tempo=119.9	Danceability=0.792	Energy=0.728	Valence=0.811	Tag+=4/4:Tempo	.Edit=	Time=03/12/2021 12:29:53	.Edit=	User=dwgray	Time=02-Apr-2021 08:14:56 PM	DanceRating=BCH+1	Tag+=Bachata:Dance	.Edit=	User=dwgray	Time=02-Apr-2021 08:16:06 PM	DanceRating=BCH-2	Tag-=Bachata:Dance	Tag+=!Bachata:Dance",
                await GetService());
            Assert.IsFalse(song.TagSummary.HasTag("Bachata:Dance"));
        }

        [TestMethod]
        public async Task SpotifyCreate()
        {
            var service = await GetService();
            var track = new ServiceTrack
            {
                Album = "Greatest Hits",
                Artist = "The Beach Boys",
                CollectionId = "2ninxvLuYGCb6H92qTaSFZ",
                Duration = 154,
                Name = "Wouldn't It Be Nice",
                Service = ServiceType.Spotify,
                TrackId = "6VojZJpMyuKClbwyilWlQj",
                TrackNumber = 4
            };

            var song = await Song.CreateFromTrack(
                service,
                await service.FindUser("dwgray"),
                track, "WCS", "Testing:Other|Crazy:Music",
                "Dances:Style|Mellow:Tempo");

            var actual = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
            //Trace.WriteLine(actual);

            const string expected =
                @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Length=154	Album:00=Greatest Hits	Track:00=4	Tag+=Crazy:Music|Testing:Other|West Coast Swing:Dance	DanceRating=WCS+1	Tag+:WCS=Dances:Style|Mellow:Tempo	Purchase:00:SA=2ninxvLuYGCb6H92qTaSFZ	Purchase:00:SS=6VojZJpMyuKClbwyilWlQj	DanceRating=SWG+1";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task AdminModifyMga()
        {
            var service = await GetService();
            const string init =
                @".Create=	User=dwgray	Time=06/11/2017 17:17:51	Title=El distinguido ciudadano (The Distinguished Citizen) [1940]	Artist=Edgardo Donato & his orchestra	Length=149	Album:00=The best of Argentine Tango Vol. 2 / 78 rpm recordings 1927 - 1957	Track:00=17	Tag+=Argentine Tango:Dance	DanceRating=ATN+2	Purchase:00:SS=0ns4CpHDRcexy62FEQP0Fh	DanceRating=TNG+1	.Edit=	User=batch-a	Time=06/11/2017 17:23:40	Purchase:00:AS=D:B001VL341Y	Purchase:00:AA=D:B001VL4RKQ	Tag+=Latin:Music	.Edit=	User=batch-i	Time=06/11/2017 17:23:40	Album:01=The Best of Argentine Tango, Vol. 2 - 78 Rpm Recordings 1927-1957	Track:01=17	Purchase:01:IS=307599641	Purchase:01:IA=307599529	Tag+=Raíces:Music	.Edit=	User=batch-s	Time=06/11/2017 17:23:40	Purchase:00:SS=0ns4CpHDRcexy62FEQP0Fh[AD,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,ID,IE,IS,IT,JP,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,SE,SG,SK,SV,TR,TW,US,UY]	Purchase:00:SA=1MCPFcQJSIrQ45lTjfxkYn	.Edit=	User=batch	Time=06/11/2017 17:28:52	Tempo=134.4	Danceability=0.717	Energy=0.378	Valence=0.693	Tag+=4/4:Tempo	.Edit=	User=batch-s	Time=06/11/2017 17:29:13	Sample=https://p.scdn.co/mp3-preview/92d2fe46e92d1040da43258f03f971225a7330ee?cid\<EQ>\***REMOVED***	.Edit=	User=batch	Time=07/23/2017 16:43:48	DanceRating=MGA+1";
            var song = new Song();
            await song.Load(init, service);
            await song.AdminModify(
                @"{ExcludeUsers:null,Properties:[{Name:'Tag+',Value:'Argentine Tango:Dance',Replace:'Milonga:Dance'},{Name:'DanceRating',Value:'ATN+2',Replace:'MGA+2'},{Name:'DanceRating',Value:'MGA+1',Replace:null}]}",
                service);

            Assert.AreEqual(2, song.DanceRatings.Count);
            Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "MGA"));
            Assert.AreEqual(0, song.DanceRatings.Count(dr => dr.DanceId == "ATN"));
            Assert.AreEqual(
                "4/4:Tempo:1|Latin:Music:1|Milonga:Dance:1|Raíces:Music:1",
                song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task AdminModifyVals()
        {
            var service = await GetService();
            const string init =
                @".Create=	User=11101224127	Time=01/05/2018 02:39:33	Title=Pajaro Herido	Artist=Rodolfo Biagi	Length=136	Album:00=Tango Best	Track:00=17	Tag+=Argentine Tango:Dance	DanceRating=ATN+2	Purchase:00:SA=0OysrEZzotITS0fQ22yMne	Purchase:00:SS=7AddIMmrMNrAvfeVLggbdj	DanceRating=TNG+1	.Edit=	User=batch-a	Time=01/05/2018 02:41:52	Album:01=Rodolfo Biagi Con Sus Cantores: 1939-1947	Track:01=10	Purchase:01:AS=D:B075V5L1WH	Purchase:01:AA=D:B075V711N7	Album:02=The Essence of Tango: Rodolfo Biagi, Vol. 1	Track:02=3	Purchase:02:AS=D:B019EPP092	Purchase:02:AA=D:B019EPQLDQ	Album:03=Tango Classics 076: Cuatro palabras	Track:03=6	Purchase:03:AS=D:B004UPEU52	Purchase:03:AA=D:B004UPE43K	Album:04=A la luz del candil (1941 - 1943)	Track:04=12	Purchase:04:AS=D:B071DNT826	Purchase:04:AA=D:B0713R81XC	Tag+=International:Music|Latin:Music	.Edit=	User=batch-i	Time=01/05/2018 02:41:52	Purchase:04:IS=1231281699	Purchase:04:IA=1231281637	Album:05=Cuatro Palabras	Track:05=6	Purchase:05:IS=429503329	Purchase:05:IA=429503294	Tag+=Latino:Music|World:Music	.Edit=	User=batch-e	Time=01/05/2018 02:41:52	Tempo=107.1	Danceability=0.517	Energy=0.31	Valence=0.692	Tag+=3/4:Tempo";
            var song = new Song();
            await song.Load(init, service);
            await song.AdminModify(
                @"{ExcludeUsers:null,Properties:[{Name:'Tag+',Value:'Argentine Tango:Dance',Replace:'Tango Vals:Dance'},{Name:'DanceRating',Value:'ATN+2',Replace:'TGV+2'},{Name:'DanceRating',Value:'MGA+1',Replace:null},{Name:'DanceRating',Value:'TNG+1',Replace:null}]}",
                service);

            Assert.AreEqual(1, song.DanceRatings.Count);
            Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "TGV"));
            Assert.AreEqual(0, song.DanceRatings.Count(dr => dr.DanceId == "TNG"));
            Assert.AreEqual(0, song.DanceRatings.Count(dr => dr.DanceId == "ATN"));
            Assert.AreEqual(
                "3/4:Tempo:1|International:Music:1|Latin:Music:2|Tango Vals:Dance:1|World:Music:1",
                song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task AdminModifyExcludeUser()
        {
            var service = await GetService();
            const string init =
                @".Create=	User=11101224127	Time=01/05/2018 02:39:33	Title=Pajaro Herido	Artist=Rodolfo Biagi	Length=136	Album:00=Tango Best	Track:00=17	Tag+=Argentine Tango:Dance	DanceRating=ATN+2	Purchase:00:SA=0OysrEZzotITS0fQ22yMne	Purchase:00:SS=7AddIMmrMNrAvfeVLggbdj	DanceRating=TNG+1	.Edit=	User=DWTS	Time=05/13/2015 14:09:23	Tag+=Argentine Tango:Dance|DWTS:Other|Episode 9:Other|Season 20:Other|United States:Other	DanceRating=ATN+2	DanceRating=TNG+1	Tag+:ATN=Allison:Other|Riker:Other	.Edit=	User=batch-a	Time=01/05/2018 02:41:52	Album:01=Rodolfo Biagi Con Sus Cantores: 1939-1947	Track:01=10	Purchase:01:AS=D:B075V5L1WH	Purchase:01:AA=D:B075V711N7	Album:02=The Essence of Tango: Rodolfo Biagi, Vol. 1	Track:02=3	Purchase:02:AS=D:B019EPP092	Purchase:02:AA=D:B019EPQLDQ	Album:03=Tango Classics 076: Cuatro palabras	Track:03=6	Purchase:03:AS=D:B004UPEU52	Purchase:03:AA=D:B004UPE43K	Album:04=A la luz del candil (1941 - 1943)	Track:04=12	Purchase:04:AS=D:B071DNT826	Purchase:04:AA=D:B0713R81XC	Tag+=International:Music|Latin:Music	.Edit=	User=batch-i	Time=01/05/2018 02:41:52	Purchase:04:IS=1231281699	Purchase:04:IA=1231281637	Album:05=Cuatro Palabras	Track:05=6	Purchase:05:IS=429503329	Purchase:05:IA=429503294	Tag+=Latino:Music|World:Music	.Edit=	User=batch-e	Time=01/05/2018 02:41:52	Tempo=107.1	Danceability=0.517	Energy=0.31	Valence=0.692	Tag+=3/4:Tempo";
            var song = new Song();
            await song.Load(init, service);
            await song.AdminModify(
                @"{ExcludeUsers:['DWTS'],Properties:[{Name:'Tag+',Value:'Argentine Tango:Dance',Replace:'Tango Vals:Dance'},{Name:'DanceRating',Value:'ATN+2',Replace:'TGV+2'},{Name:'DanceRating',Value:'MGA+1',Replace:null},{Name:'DanceRating',Value:'TNG+1',Replace:null}]}",
                service);

            Assert.AreEqual(3, song.DanceRatings.Count);
            Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "TGV"));
            Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "TNG"));
            Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "ATN"));
            Assert.AreEqual(
                "3/4:Tempo:1|Argentine Tango:Dance:1|DWTS:Other:1|Episode 9:Other:1|International:Music:1|Latin:Music:2|Season 20:Other:1|Tango Vals:Dance:1|United States:Other:1|World:Music:1",
                song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task TagDeleteTest()
        {
            var service = await GetService();

            const string init =
                @".Create=	User=LizaMay|P	Time=12/18/2020 12:53:42	Title=Something Sweet	Artist=Madison Beer	Length=196	Album:00=Something Sweet	Track:00=1	Tag+=West Coast Swing:Dance	DanceRating=WCS+1	Purchase:00:SA=2oIUFdMdCZB6KSxcByEbDT	Purchase:00:SS=3PlM3biKS6ZUIHojCJmElE	DanceRating=SWG+1	.Edit=	User=batch-i|P	Time=12/18/2020 13:22:21	Purchase:00:IS=1068336005	Purchase:00:IA=1068336001	Tag+=Pop:Music	.Edit=	User=batch-s|P	Time=12/18/2020 13:22:21	Tag+=Dance Pop:Music|Electropop:Music|Pop Dance:Music|Pop:Music|Post Teen Pop:Music	.Edit=	User=batch-e|P	Time=12/18/2020 13:22:21	Tempo=100.0	Danceability=0.622	Energy=0.791	Valence=0.647	Tag+=4/4:Tempo	.Edit=	Time=12/18/2020 13:22:21	Sample=https://p.scdn.co/mp3-preview/8e1b328376bb18bb1b5d27649a59f2f1e175f199?cid=***REMOVED***	.Edit=	User=StephanieLienPham|P	Time=05/09/2021 09:11:30	Tag+=West Coast Swing:Dance	DanceRating=WCS+1	DanceRating=SWG+1	.Edit=	User=dwgray	Time=23-May-2021 02:32:40 PM	DeleteTag=Swing:Dance	DeleteTag=West Coast Swing:Dance	.Edit=	User=dwgray	Time=23-May-2021 03:09:31 PM	DeleteTag=Dance Pop:Music	DeleteTag=Pop Dance:Music";
            var song = new Song();
            await song.Load(init, service);

            Assert.AreEqual(0, song.DanceRatings.Count);
            Assert.AreEqual(
                "4/4:Tempo:1|Electropop:Music:1|Pop:Music:2|Post Teen Pop:Music:1",
                song.TagSummary.ToString());
        }
    }
}
