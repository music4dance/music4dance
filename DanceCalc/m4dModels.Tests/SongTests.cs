using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongTests
    {
        [TestMethod]
        public void NormalForm()
        {
            for (int i = 0; i < titles.Length; i++)
            {
                string t = titles[i];

                string n = Song.CreateNormalForm(t);
                Assert.AreEqual<string>(normal[i], n);
                //Trace.WriteLine(string.Format("{0}",Song.CreateTitleHash(t)));
            }
        }

        [TestMethod]
        public void TitleHash()
        {
            for (int i = 0; i < hashes.Length; i++)
            {
                string t = titles[i];

                int hash = Song.CreateTitleHash(t);
                Assert.AreEqual<int>(hashes[i], hash);
            }
        }

        [TestMethod]
        public void CleanString()
        {
            for (int i = 0; i < titles.Length; i++)
            {
                string t = titles[i];

                string n = Song.CreateNormalForm(t);
                Assert.AreEqual<string>(normal[i], n);
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
        public void Ratings()
        {
            Song song = new Song();
            song.Load(@"user=batch	Title=Test	Artist=Me	Tempo=30.0",s_users,s_factories);

            string init = song.ToString();
            Trace.WriteLine(init);
            SongDetails sd = new SongDetails(song);

            DanceRatingDelta rmb = new DanceRatingDelta { DanceId = "RMB", Delta = 5 };
            DanceRatingDelta cha = new DanceRatingDelta { DanceId = "CHA", Delta = 7 };
            DanceRatingDelta fxt = new DanceRatingDelta { DanceId = "FXT", Delta = 13 };
            DanceRatingDelta swg = new DanceRatingDelta { DanceId = "SWG", Delta = 27 };
            DanceRatingDelta ch2 = new DanceRatingDelta { DanceId = "CHA", Delta = -7 };
            DanceRatingDelta rm2 = new DanceRatingDelta { DanceId = "RMB", Delta = 200 };

            sd.UpdateDanceRating(rmb);
            sd.UpdateDanceRating(cha);
            sd.UpdateDanceRating(fxt);

            // Create an test an initial small list of dance ratings
            ApplicationUser user = s_users.FindUser("dwgray");
            song.Update(user, sd, s_factories, s_users);
            string first = song.ToString();
            Trace.WriteLine(first);
            Assert.IsTrue(song.DanceRatings.Count == 3);

            // Now mix it up a bit

            sd.UpdateDanceRating(ch2);
            sd.UpdateDanceRating(rm2);
            string delta = song.ToString();
            Trace.WriteLine(delta);
            Assert.IsTrue(song.DanceRatings.Count == 3);
            DanceRating drT = song.FindRating("RMB");
            Assert.IsTrue(drT.Weight == 105);
        }
        static string[] titles = new string[] {
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

        static string[] normal = new string[] {
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

        static string[] clean = new string[] {
            "ñ é á",
            "Señor Bolero",
            "Viaje Tiemp Atrás",
            "Solo Tu",
            "Señales De Humo",
            "España Cañi",
            "Y Qu les Filles Qui M interessent",
            "Namorádo",
            "Satisfaction (I Can t Get No)",
            "Satisfaction",
            "Moliendo Café",
            "This is Life",
            "How Stranger can Live",
            "Can't for's real"
        };

        static int[] hashes = new int[] {
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

        static MockUserMap s_users = new MockUserMap();
        static MockFactories s_factories = new MockFactories();
    }
}
