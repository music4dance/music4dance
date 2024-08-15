using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongTests
    {
        private const string Vals =
            @".Create=	User=11101224127	Time=01/05/2018 02:39:33	Title=Pajaro Herido	Artist=Rodolfo Biagi	Length=136	Album:00=Tango Best	Track:00=17	Tag+=Argentine Tango:Dance	DanceRating=ATN+2	Purchase:00:SA=0OysrEZzotITS0fQ22yMne	Purchase:00:SS=7AddIMmrMNrAvfeVLggbdj	DanceRating=TNG+1	.Edit=	User=batch-a	Time=01/05/2018 02:41:52	Album:01=Rodolfo Biagi Con Sus Cantores: 1939-1947	Track:01=10	Purchase:01:AS=D:B075V5L1WH	Purchase:01:AA=D:B075V711N7	Album:02=The Essence of Tango: Rodolfo Biagi, Vol. 1	Track:02=3	Purchase:02:AS=D:B019EPP092	Purchase:02:AA=D:B019EPQLDQ	Album:03=Tango Classics 076: Cuatro palabras	Track:03=6	Purchase:03:AS=D:B004UPEU52	Purchase:03:AA=D:B004UPE43K	Album:04=A la luz del candil (1941 - 1943)	Track:04=12	Purchase:04:AS=D:B071DNT826	Purchase:04:AA=D:B0713R81XC	Tag+=International:Music|Latin:Music	.Edit=	User=batch-i	Time=01/05/2018 02:41:52	Purchase:04:IS=1231281699	Purchase:04:IA=1231281637	Album:05=Cuatro Palabras	Track:05=6	Purchase:05:IS=429503329	Purchase:05:IA=429503294	Tag+=Latino:Music|World:Music	.Edit=	User=batch-e	Time=01/05/2018 02:41:52	Tempo=107.1	Danceability=0.517	Energy=0.31	Valence=0.692	Tag+=3/4:Tempo";

        private const string Tango =
            @".Create=	User=DWTS|P	Time=09/23/2016 09:37:20	Tag+=DWTS:Other|Episode 2:Other|Season 23:Other|Tango:Dance|United States:Other	DanceRating=TNG+1	Tag+:TNG=Gleb:Other|Jana:Other	Title=I Don't Want to Be	Artist=Gavin DeGraw";
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
        }


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
                @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Wouldn't It Be Nice	Artist=The Beach Boys	Length=154	Album:00=Greatest Hits	Track:00=4	Tag+=Crazy:Music|Testing:Other|West Coast Swing:Dance	DanceRating=WCS+1	Tag+:WCS=Dances:Style|Mellow:Tempo	Purchase:00:SA=2ninxvLuYGCb6H92qTaSFZ	Purchase:00:SS=6VojZJpMyuKClbwyilWlQj";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public async Task AdminModifyMga()
        {
            var service = await GetService();
            const string init =
                @".Create=	User=dwgray	Time=06/11/2017 17:17:51	Title=El distinguido ciudadano (The Distinguished Citizen) [1940]	Artist=Edgardo Donato & his orchestra	Length=149	Album:00=The best of Argentine Tango Vol. 2 / 78 rpm recordings 1927 - 1957	Track:00=17	Tag+=Argentine Tango:Dance	DanceRating=ATN+2	Purchase:00:SS=0ns4CpHDRcexy62FEQP0Fh	DanceRating=TNG+1	.Edit=	User=batch-a	Time=06/11/2017 17:23:40	Purchase:00:AS=D:B001VL341Y	Purchase:00:AA=D:B001VL4RKQ	Tag+=Latin:Music	.Edit=	User=batch-i	Time=06/11/2017 17:23:40	Album:01=The Best of Argentine Tango, Vol. 2 - 78 Rpm Recordings 1927-1957	Track:01=17	Purchase:01:IS=307599641	Purchase:01:IA=307599529	Tag+=Raíces:Music	.Edit=	User=batch-s	Time=06/11/2017 17:23:40	Purchase:00:SS=0ns4CpHDRcexy62FEQP0Fh[AD,AT,AU,BE,BG,BO,BR,CA,CH,CL,CO,CR,CY,CZ,DE,DK,DO,EC,EE,ES,FI,FR,GB,GR,GT,HK,HN,HU,ID,IE,IS,IT,JP,LI,LT,LU,LV,MC,MT,MX,MY,NI,NL,NO,NZ,PA,PE,PH,PL,PT,PY,SE,SG,SK,SV,TR,TW,US,UY]	Purchase:00:SA=1MCPFcQJSIrQ45lTjfxkYn	.Edit=	User=batch	Time=06/11/2017 17:28:52	Tempo=134.4	Danceability=0.717	Energy=0.378	Valence=0.693	Tag+=4/4:Tempo	.Edit=	User=batch-s	Time=06/11/2017 17:29:13	Sample=https://p.scdn.co/mp3-preview/92d2fe46e92d1040da43258f03f971225a7330ee?cid\<EQ>\e6dc118cd7604cd2b8bd0a979a18e6f8	.Edit=	User=batch	Time=07/23/2017 16:43:48	DanceRating=MGA+1";
            var song = new Song();
            await song.Load(init, service);
            await song.AdminModify(
                @"{ExcludeUsers:null,Properties:[{Action:'ReplaceValue',Name:'Tag+',Value:'Argentine Tango:Dance',Replace:'Milonga:Dance'},{Action:'ReplaceValue',Name:'DanceRating',Value:'ATN+2',Replace:'MGA+2'},{Action:'Remove',Name:'DanceRating',Value:'MGA+1'}]}",
                service);

            Assert.AreEqual(2, song.DanceRatings.Count);
            Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "MGA"));
            Assert.AreEqual(0, song.DanceRatings.Count(dr => dr.DanceId == "ATN"));
            Assert.AreEqual(
                "4/4:Tempo:1|Latin:Music:1|Milonga:Dance:1|Raíces:Music:1",
                song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task CompressTags()
        {
            var service = await GetService();
            var song = new Song();
            await song.Load(Vals, service);

            var initSummary = song.TagSummary.ToString();
            Assert.AreEqual(51, song.SongProperties.Count);
            Trace.WriteLine($"Initial Count: {song.SongProperties.Count}");
            Assert.IsTrue(await song.ExpandTags(service));
            Trace.WriteLine($"Expanded Count: {song.SongProperties.Count}");
            Assert.AreEqual(53, song.SongProperties.Count);
            Assert.IsTrue(await song.CollapseTags(service));
            Trace.WriteLine($"Collapsed Count: {song.SongProperties.Count}");
            Assert.AreEqual(51, song.SongProperties.Count);
            Assert.AreEqual(initSummary, song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task AdminModifyVals()
        {
            var service = await GetService();
            var song = new Song();
            await song.Load(Vals, service);
            await song.AdminModify(
                @"{Properties:[{Action:'ReplaceValue',Name:'Tag+',Value:'Argentine Tango:Dance',Replace:'Tango Vals:Dance'},{Action:'ReplaceValue',Name:'DanceRating',Value:'ATN+2',Replace:'TGV+2'},{Action:'Remove',Name:'DanceRating',Value:'MGA+1'},{Action:'Remove',Name:'DanceRating',Value:'TNG+1'}]}",
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
        public async Task AdminModifyTango()
        {
            var service = await GetService();
            var song = new Song();
            await song.Load(Tango, service);
            await song.AdminModify(
                @"{Properties:[{Action:'ReplaceValue',Name:'Tag+',Value:'Tango:Dance',Replace:'Neo Tango:Dance'},{Action:'ReplaceValue',Name:'DanceRating',Value:'TNG',Replace:'NTN'}]}",
                service);

            Assert.AreEqual(1, song.DanceRatings.Count);
            Assert.AreEqual(1, song.DanceRatings.Count(dr => dr.DanceId == "NTN"));
            Assert.AreEqual(0, song.DanceRatings.Count(dr => dr.DanceId == "TNG"));
            Assert.IsTrue(song.SongProperties.Exists(p => p.Name == "Tag+:NTN"));
            Assert.AreEqual(
                song.SongProperties.Find(p => p.Name == "Tag+:NTN")?.Value,
                "Gleb:Other|Jana:Other");
        }

        [TestMethod]
        public void BuildModifierWithDanceRating()
        {
            var modifier = SongModifier.Build(
                @"{ExcludeUsers:null,Properties:[{Name:'DanceRating',Value:'ATN',Replace:'TGV'}]}");

            Assert.AreEqual(modifier.Properties.Count, 3);
            Assert.IsTrue(modifier.Properties.Exists(p => p.Name == "Tag+:ATN"));
            Assert.IsTrue(modifier.Properties.Exists(p => p.Name == "Tag-:ATN"));
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
                @"{ExcludeUsers:['dwts'],Properties:[{Action:'ReplaceValue',Name:'Tag+',Value:'Argentine Tango:Dance',Replace:'Tango Vals:Dance'},{Action:'ReplaceValue',Name:'DanceRating',Value:'ATN',Replace:'TGV'},{Action:'Remove',Name:'DanceRating',Value:'MGA'},{Action:'Remove',Name:'DanceRating',Value:'TNG'}]}",
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
        public async Task AdminAddUserProperties()
        {
            var service = await GetService();
            const string init =
                @".Create=	User=Amstl|P	Time=11/27/2021 09:01:22	Title=Santa Claus Is Comin' To Town - (Cha Cha Cha / 32 BPM)	Artist=Tanz Orchester Klaus Hallen	Length=217	Album:00=Christmas	Track:00=3	Tag+=Cha Cha:Dance	DanceRating=CHA+1	Purchase:00:SA=7jy1ckFTNouRlg7PfTBrCc	Purchase:00:SS=1eUvYOYBDuTBX9uStaCXfB	.Edit=	User=batch-i|P	Time=11/27/2021 09:01:23	Purchase:00:IS=504512032	Purchase:00:IA=504512003	Tag+=Pop:Music	.Edit=	User=Amstl|P	Time=11/27/2021 09:02:53	Tag+=Rumba:Dance	DanceRating=RMB+1	.Edit=	User=batch-s|P	Time=04/04/2023 16:58:27	Tempo=128.0	Danceability=0.803	Energy=0.591	Valence=0.935	Tag+=4/4:Tempo";
            var song = new Song();
            await song.Load(init, service);

            Trace.WriteLine(song.TagSummary);
            Assert.AreEqual(@"4/4:Tempo:1|Cha Cha:Dance:1|Pop:Music:1|Rumba:Dance:1", song.TagSummary.ToString());

            bool changed = await song.AdminAddUserProperties("Amstl|P",
                [new SongProperty(Song.AddedTags, "Holiday:Other|Christmas:Other")],
                service);

            Assert.IsTrue(changed);

            Trace.WriteLine(song.TagSummary);
            Assert.AreEqual(@"4/4:Tempo:1|Cha Cha:Dance:1|Christmas:Other:1|Holiday:Other:1|Pop:Music:1|Rumba:Dance:1", song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task AdminAddUserPropertiesEnd()
        {
            var service = await GetService();
            const string init =
                @".Create=	User=Amstl|P	Time=11/27/2021 09:01:22	Title=Santa Claus Is Comin' To Town - (Cha Cha Cha / 32 BPM)	Artist=Tanz Orchester Klaus Hallen	Length=217	Album:00=Christmas	Track:00=3	Tag+=Cha Cha:Dance	DanceRating=CHA+1	Purchase:00:SA=7jy1ckFTNouRlg7PfTBrCc	Purchase:00:SS=1eUvYOYBDuTBX9uStaCXfB";
            var song = new Song();
            await song.Load(init, service);

            Trace.WriteLine(song.TagSummary);
            Assert.AreEqual(@"Cha Cha:Dance:1", song.TagSummary.ToString());

            bool changed = await song.AdminAddUserProperties("Amstl|P",
                [new SongProperty(Song.AddedTags, "Holiday:Other|Christmas:Other")],
                service);
            Assert.IsTrue(changed);

            Trace.WriteLine(song.TagSummary);
            Assert.AreEqual(@"Cha Cha:Dance:1|Christmas:Other:1|Holiday:Other:1", song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task AdminAddUserPropertiesDuplicate()
        {
            var service = await GetService();
            const string init =
                @".Create=	User=Amstl|P	Time=11/27/2021 09:01:22	Title=Santa Claus Is Comin' To Town - (Cha Cha Cha / 32 BPM)	Artist=Tanz Orchester Klaus Hallen	Length=217	Album:00=Christmas	Track:00=3	Tag+=Cha Cha:Dance	DanceRating=CHA+1	Purchase:00:SA=7jy1ckFTNouRlg7PfTBrCc	Purchase:00:SS=1eUvYOYBDuTBX9uStaCXfB	Tag+=Holiday:Other|Christmas:Other";
            var song = new Song();
            await song.Load(init, service);

            Trace.WriteLine(song.TagSummary);
            Assert.AreEqual(@"Cha Cha:Dance:1|Christmas:Other:1|Holiday:Other:1", song.TagSummary.ToString());

            bool changed = await song.AdminAddUserProperties("Amstl|P",
                [new SongProperty(Song.AddedTags, "Holiday:Other|Christmas:Other")],
                service);
            Assert.IsFalse(changed);

            Trace.WriteLine(song.TagSummary);
            Assert.AreEqual(@"Cha Cha:Dance:1|Christmas:Other:1|Holiday:Other:1", song.TagSummary.ToString());
        }

        [TestMethod]
        public async Task TagDeleteTest()
        {
            var service = await GetService();

            const string init =
                @".Create=	User=LizaMay|P	Time=12/18/2020 12:53:42	Title=Something Sweet	Artist=Madison Beer	Length=196	Album:00=Something Sweet	Track:00=1	Tag+=West Coast Swing:Dance	DanceRating=WCS+1	Purchase:00:SA=2oIUFdMdCZB6KSxcByEbDT	Purchase:00:SS=3PlM3biKS6ZUIHojCJmElE	DanceRating=SWG+1	.Edit=	User=batch-i|P	Time=12/18/2020 13:22:21	Purchase:00:IS=1068336005	Purchase:00:IA=1068336001	Tag+=Pop:Music	.Edit=	User=batch-s|P	Time=12/18/2020 13:22:21	Tag+=Dance Pop:Music|Electropop:Music|Pop Dance:Music|Pop:Music|Post Teen Pop:Music	.Edit=	User=batch-e|P	Time=12/18/2020 13:22:21	Tempo=100.0	Danceability=0.622	Energy=0.791	Valence=0.647	Tag+=4/4:Tempo	.Edit=	Time=12/18/2020 13:22:21	Sample=https://p.scdn.co/mp3-preview/8e1b328376bb18bb1b5d27649a59f2f1e175f199?cid=e6dc118cd7604cd2b8bd0a979a18e6f8	.Edit=	User=StephanieLienPham|P	Time=05/09/2021 09:11:30	Tag+=West Coast Swing:Dance	DanceRating=WCS+1	DanceRating=SWG+1	.Edit=	User=dwgray	Time=23-May-2021 02:32:40 PM	DeleteTag=Swing:Dance	DeleteTag=West Coast Swing:Dance	.Edit=	User=dwgray	Time=23-May-2021 03:09:31 PM	DeleteTag=Dance Pop:Music	DeleteTag=Pop Dance:Music";
            var song = new Song();
            await song.Load(init, service);

            Assert.AreEqual(0, song.DanceRatings.Count);
            Assert.AreEqual(
                "4/4:Tempo:1|Electropop:Music:1|Pop:Music:2|Post Teen Pop:Music:1",
                song.TagSummary.ToString());
        }

        const string UndoSong = @".Create=	User=dgsnure|P	Time=03/30/2019 09:01:27	Title=all the good girls go to hell	Artist=Billie Eilish	Length=169	Album:00=WHEN WE ALL FALL ASLEEP, WHERE DO WE GO?	Track:00=5	Tag+=Cha Cha:Dance	DanceRating=CHA+1	.Edit=	User=SabrinaSkandy|P	Time=01/26/2022 03:34:57	Tag+=West Coast Swing:Dance	DanceRating=WCS+1	.Edit=	User=Charlie	Time=11-Sep-2023 07:26:08 PM	Like=true	.Edit=	User=JuliaS	Time=17-May-2024 03:09:42 PM	Like=true	.Edit=	User=Charlie	Time=15-Aug-2024 08:47:03 AM	DanceRating=CHA+1	Tag+=Cha Cha:Dance	Comment+:WCS=I real swing out to this one.";

        [TestMethod]
        public async Task UndoUser()
        {
            var service = await GetService();
            var user = await service.FindUser("JuliaS");

            var song = new Song();
            await song.Load(UndoSong, service);

            Assert.IsNotNull(song.FindModified(user.UserName));
            var c = song.SongProperties.Count;

            Trace.WriteLine($"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var success = await song.UndoUserChanges(user, service);
            Trace.WriteLine($"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);

            Assert.IsTrue(success);

            Assert.IsNull(song.FindModified(user.UserName));
            Assert.AreEqual(c - 4, song.SongProperties.Count);
        }

        [TestMethod]
        public async Task UndoUserMulti()
        {
            var service = await GetService();
            var user = await service.FindUser("charlie");

            var song = new Song();
            await song.Load(UndoSong, service);
            var c = song.SongProperties.Count;

            Assert.IsNotNull(song.FindModified(user.UserName));

            Trace.WriteLine($"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var success = await song.UndoUserChanges(user, service);
            Trace.WriteLine($"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            Assert.IsTrue(success);

            Assert.IsNull(song.FindModified(user.UserName));
            Assert.AreEqual(c - 10, song.SongProperties.Count);
        }

    }
}
