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
                string str = s.ToString().Trim();
                string org = s_data[i].Trim();
                Trace.WriteLine(org);
                Trace.WriteLine(str);
                if (!string.Equals(org,str))
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
                Assert.AreEqual(org,str,string.Format("{0} failed to save.",s.SongId.ToString("B")));
            }
        }

        static IList<SongDetails> Load()
        {
            List<SongDetails> songs = new List<SongDetails>();
            foreach (string str in s_data)
            {
                songs.Add(new SongDetails(str, null));
            }

            return songs;
        }

        static string[] s_data =
        {
            @"SongId={ea55fcea-35f5-4d0d-81b5-a5264395945d}	Purchase:1:IA=554530	User=batch	Time=5/21/2014 9:15:26 PM	Length=155	Purchase:1:AS=D:B000W0CTAW	Purchase:1:AA=D:B000W0B00W	Purchase:1:IS=554314	Genre=Jazz	Length=156	Time=5/21/2014 7:16:49 PM	User=batch	PromoteAlbum:1=	Purchase:1:XS=music.B76B0F00-0100-11DB-89CA-0019B92A3933	Track:1=5	Album:1=Sings Great American Songwriters	Genre=Pop	Length=155	Time=5/21/2014 2:04:51 PM	User=batch	DanceRating=FXT+5	Album:0=The Ultimate Ballroom Album 12	Tempo=116.0	Artist=Carmen McRae	Title=Blue Moon	Time=3/17/2014 5:43:50 PM	User=SalsaSwingBallroom",
            @"SongId={7471bb23-cf92-468f-9fb6-e6031571f29a}	User=SalsaSwingBallroom	Time=3/17/2014 5:45:32 PM	Title=Blue Moon	Artist=Carmen Mccrae	Tempo=112.0	DanceRating=ECS+5	User=batch	Time=7/11/2014 9:49:28 PM	Artist=Carmen McRae	Length=158	Genre=Jazz	Album:00=Greatest Hits	Track:00=19	Purchase:00:XS=music.18D74B06-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch	Time=7/11/2014 9:50:07 PM	Length=156	Album:01=Sings Great American Songwriters	Track:01=5	Purchase:01:IS=554314	Purchase:01:IA=554530	PromoteAlbum:01=	User=batch	Time=7/11/2014 9:50:35 PM	Length=155	Purchase:01:AS=D:B000W0CTAW	Purchase:01:AA=D:B000W0B00W",
            @"SongId={0b1e4225-d782-41d1-9f16-b105e7bd0efa}	User=breanna	Time=6/10/2014 3:11:03 PM	Title=Lady Marmalade	Artist=Christina Aguilera	DanceRating=CHA+5	User=batch	Time=6/10/2014 3:26:26 PM	Length=264	Genre=Pop	Album:00=Moulin Rouge	Track:00=2	Purchase:00:XS=music.9F480F00-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=",
            @"SongId={52cb6e8c-6f0f-469e-ac83-d353cbab6c96}	User=breanna	Time=6/9/2014 8:54:43 PM	Title=Lady Marmalade	Artist=Christina Aguilera, Mya, Pink,  & Lil Kim	DanceRating=HST+5	User=batch	Time=7/4/2014 9:54:35 PM	Artist=Christina Aguilera	Length=264	Genre=Pop	Album:00=Moulin Rouge	Track:00=2	Purchase:00:XS=music.9F480F00-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch	Time=7/4/2014 9:55:04 PM	Length=265	Album:00=Moulin Rouge (Soundtrack from the Motion Picture)	Purchase:00:IS=3577756	Purchase:00:IA=3579609	User=batch	Time=7/4/2014 9:55:49 PM	Album:01=Music From Nicole Kidman Movies	Track:01=3	Purchase:01:AS=D:B004XOHIH2	Purchase:01:AA=D:B004XOHHXM	PromoteAlbum:01=",
        };

        //static MockUserMap s_users = new MockUserMap();
    };
}
