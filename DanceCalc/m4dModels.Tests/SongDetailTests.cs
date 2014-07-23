using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System.Diagnostics;
using m4dModels;
using System.Collections.Generic;
using System.Diagnostics;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongDetailTests
    {
        [TestMethod]
        public void TitleArtistMatch()
        {
            SongDetails sd1 = new SongDetails { Title = "A Song (With a subtitle)", Artist = "Crazy Artist" };
            SongDetails sd2 = new SongDetails { Title = "Moliendo Café", Artist = "The Who" };
            SongDetails sd3 = new SongDetails { Title = "If the song or not", Artist = "Señor Bolero" };

            Assert.IsTrue(sd1.TitleArtistMatch("A Song (With a subtitle)","Crazy Artist"),"SD1: Exact");
            Assert.IsTrue(sd1.TitleArtistMatch("Song", "Crazy Artist"), "SD1: Weak");
            Assert.IsFalse(sd1.TitleArtistMatch("Song", "Crazy Artiste"), "SD1: No Match");

            Assert.IsTrue(sd2.TitleArtistMatch("Moliendo Café", "The Who"), "SD2: Exact");
            Assert.IsTrue(sd2.TitleArtistMatch("Moliendo Cafe", "Who"), "SD2: Weak");
            Assert.IsFalse(sd2.TitleArtistMatch("Molienda Café", "The Who"), "SD2: No Match");

            Assert.IsTrue(sd3.TitleArtistMatch("If the song or not", "Señor Bolero"), "SD3: Exact");
            Assert.IsTrue(sd3.TitleArtistMatch("If  Song and  NOT", "Senor Bolero "), "SD3: Weak");
            Assert.IsFalse(sd3.TitleArtistMatch("If the song with not", "Señor Bolero"), "SD3: No Match");
        }

        [TestMethod]
        public void LoadingDetails()
        {
            var songs = Load();
            Assert.AreEqual(s_data.Length, songs.Count);
        }

        [TestMethod]
        public void SavingDetails()
        {
            var songs = Load();
            Assert.AreEqual(songs.Count, s_data.Length);
            for (int i = 0; i < songs.Count; i++)
            {
                SongDetails s = songs[i];
                DiffSerialized(s.ToString(), s_data[i], s.SongId);
            }
        }

        private static void DiffSerialized(string org, string str, Guid id)
        {
            str = str.Trim();
            org = org.Trim();
            Trace.WriteLine(org);
            Trace.WriteLine(str);

            if (!string.Equals(org, str))
            {
                if (org.Length == str.Length)
                {
                    for (int ich = 0; ich < str.Length; ich++)
                    {
                        if (org[ich] != str[ich])
                        {
                            Trace.WriteLine(string.Format("Results differ starting at {0}", ich));
                            break;
                        }
                    }
                }
                else if (org.Length < str.Length)
                {
                    Trace.WriteLine("Org shorter than result");
                }
                else
                {
                    Trace.WriteLine("Result shorter than org");
                }
            }
            Assert.AreEqual(org,str,string.Format("{0} failed to save.",id.ToString("B")));
        }

        [TestMethod]
        public void LoadingRowDetails()
        {
            var songs = LoadRows();
            Assert.AreEqual(s_rows.Length, songs.Count);

            for (int i = 0; i < s_rowProps.Length; i++ )
            {
                var song = songs[i];
                //Trace.WriteLine(song.Serialize(new string[] { SongBase.NoSongId }));
                Assert.AreEqual(s_rowProps[i], song.Serialize(new string[] { SongBase.NoSongId }));
            }
        }

        [TestMethod]
        public void CreatingSongs()
        {
            var songs = LoadRows();
            Assert.AreEqual(s_rows.Length, songs.Count);
            ApplicationUser user = s_users.FindUser("dwgray");

            for (int i = 0; i < s_rowProps.Length; i++)
            {
                SongDetails sd = songs[i];

                Song s = new Song() { SongId = sd.SongId };
                s.Create(sd, user, SongBase.CreateCommand, null, s_factories, s_users);

                string txt = s.Serialize(new string[] { SongBase.NoSongId });
                //Trace.WriteLine(txt);

                Assert.IsTrue(txt.StartsWith("User=dwgray\t"));
                string[] r = txt.Split(new char[] { '\t' });
                List<string> l = new List<string>(r);
                l.RemoveRange(0,2);
                txt = string.Join("\t",l);
                Assert.AreEqual(s_rowProps[i], txt);
                //Trace.WriteLine(txt);
            }
        }

        static IList<SongDetails> Load()
        {
            List<SongDetails> songs = new List<SongDetails>();
            foreach (string str in s_data)
            {
                songs.Add(new SongDetails(str));
            }

            return songs;
        }

        static IList<SongDetails> LoadRows()
        {
            IList<string> headers = SongDetails.BuildHeaderMap(s_header);
            IList<SongDetails> ret = SongDetails.CreateFromRows("\t",headers,s_rows,5);
            return ret;
        }

        static string[] s_data =
        {
            @"SongId={ea55fcea-35f5-4d0d-81b5-a5264395945d}	Purchase:1:IA=554530	User=batch	Time=5/21/2014 9:15:26 PM	Length=155	Purchase:1:AS=D:B000W0CTAW	Purchase:1:AA=D:B000W0B00W	Purchase:1:IS=554314	Genre=Jazz	Length=156	Time=5/21/2014 7:16:49 PM	User=batch	PromoteAlbum:1=	Purchase:1:XS=music.B76B0F00-0100-11DB-89CA-0019B92A3933	Track:1=5	Album:1=Sings Great American Songwriters	Genre=Pop	Length=155	Time=5/21/2014 2:04:51 PM	User=batch	DanceRating=FXT+5	Album:0=The Ultimate Ballroom Album 12	Tempo=116.0	Artist=Carmen McRae	Title=Blue Moon	Time=3/17/2014 5:43:50 PM	User=SalsaSwingBallroom",
            @"SongId={7471bb23-cf92-468f-9fb6-e6031571f29a}	User=dwgray	Time=3/17/2014 5:45:32 PM	Title=Blue Moon	Artist=Carmen Mccrae	Tempo=112.0	DanceRating=ECS+5	User=batch	Time=7/11/2014 9:49:28 PM	Artist=Carmen McRae	Length=158	Genre=Jazz	Album:00=Greatest Hits	Track:00=19	Purchase:00:XS=music.18D74B06-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch	Time=7/11/2014 9:50:07 PM	Length=156	Album:01=Sings Great American Songwriters	Track:01=5	Purchase:01:IS=554314	Purchase:01:IA=554530	PromoteAlbum:01=	User=batch	Time=7/11/2014 9:50:35 PM	Length=155	Purchase:01:AS=D:B000W0CTAW	Purchase:01:AA=D:B000W0B00W",
            @"SongId={0b1e4225-d782-41d1-9f16-b105e7bd0efa}	User=dwgray	Time=6/10/2014 3:11:03 PM	Title=Lady Marmalade	Artist=Christina Aguilera	DanceRating=CHA+5	User=batch	Time=6/10/2014 3:26:26 PM	Length=264	Genre=Pop	Album:00=Moulin Rouge	Track:00=2	Purchase:00:XS=music.9F480F00-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=",
            @"SongId={52cb6e8c-6f0f-469e-ac83-d353cbab6c96}	User=batch	Time=6/9/2014 8:54:43 PM	Title=Lady Marmalade	Artist=Christina Aguilera, Mya, Pink,  & Lil Kim	DanceRating=HST+5	User=batch	Time=7/4/2014 9:54:35 PM	Artist=Christina Aguilera	Length=264	Genre=Pop	Album:00=Moulin Rouge	Track:00=2	Purchase:00:XS=music.9F480F00-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch	Time=7/4/2014 9:55:04 PM	Length=265	Album:00=Moulin Rouge (Soundtrack from the Motion Picture)	Purchase:00:IS=3577756	Purchase:00:IA=3579609	User=batch	Time=7/4/2014 9:55:49 PM	Album:01=Music From Nicole Kidman Movies	Track:01=3	Purchase:01:AS=D:B004XOHIH2	Purchase:01:AA=D:B004XOHHXM	PromoteAlbum:01=",
        };

        static string s_header = @"Title	Artist	BPM	Dance	Album	AMAZONTRACK";
        static string[] s_rows =
        {
            @"Black Sheep	Gin Wigmore	30	WCS	Gravel & Wine [+digital booklet]	B00BYKXC82",
            @"The L Train	Gabriel Yared	26	SWZ	Shall We Dance?	B001NYTZJY",
            @"Come Wake Me Up	Rascal Flatts	51	VWZ	Changed (Deluxe Version) [+Digital Booklet]	B007MSUAV2",
            @"Inflitrado	Bajofondo	30	TNG	Mar Dulce	B001C3G8MS",
            @"Des Croissants de Soleil	Emilie-Claire Barlow	24	RMBI,BOL	Des croissants de soleil	B009CW0JFS",
            @"Private Eyes	Brazilian Love Affair	31	RMBA	Brazilian Lounge - Les Mysteres De Rio	B007UK5L52",
            @"Glam	Dimie Cat	50	QST	Glam!	B0042D1W6C",
            @"All For You	Imelda May	30	SFT	More Mayhem	B008VSKRAQ",
        };

        static string[] s_rowProps =
        {
            @"Title=Black Sheep	Artist=Gin Wigmore	Tempo=30.0	DanceRating=WCS+5	Album:00=Gravel & Wine [+digital booklet]	Purchase:00:AS=B00BYKXC82",
            @"Title=The L Train	Artist=Gabriel Yared	Tempo=26.0	DanceRating=SWZ+5	Album:00=Shall We Dance?	Purchase:00:AS=B001NYTZJY",
            @"Title=Come Wake Me Up	Artist=Rascal Flatts	Tempo=51.0	DanceRating=VWZ+5	Album:00=Changed (Deluxe Version) [+Digital Booklet]	Purchase:00:AS=B007MSUAV2",
            @"Title=Inflitrado	Artist=Bajofondo	Tempo=30.0	DanceRating=TNG+5	Album:00=Mar Dulce	Purchase:00:AS=B001C3G8MS",
            @"Title=Des Croissants de Soleil	Artist=Emilie-Claire Barlow	Tempo=24.0	DanceRating=RMBI+5	DanceRating=BOL+5	Album:00=Des croissants de soleil	Purchase:00:AS=B009CW0JFS",
            @"Title=Private Eyes	Artist=Brazilian Love Affair	Tempo=31.0	DanceRating=RMBA+5	Album:00=Brazilian Lounge - Les Mysteres De Rio	Purchase:00:AS=B007UK5L52",
            @"Title=Glam	Artist=Dimie Cat	Tempo=50.0	DanceRating=QST+5	Album:00=Glam!	Purchase:00:AS=B0042D1W6C",
            @"Title=All For You	Artist=Imelda May	Tempo=30.0	DanceRating=SFT+5	Album:00=More Mayhem	Purchase:00:AS=B008VSKRAQ",
        };

        static MockUserMap s_users = new MockUserMap();
        static MockFactories s_factories = new MockFactories();
    };
}
