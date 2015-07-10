using System.Diagnostics;
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

            var init = song.ToString();
            //Trace.WriteLine(init);
            var sd = new SongDetails(song);

            sd.UpdateDanceRatings(new[] {"RMB","CHA"}, 5);
            sd.UpdateDanceRatings(new[] { "FXT" }, 7);

            // Create an test an initial small list of dance ratings
            var user = Service.FindUser("dwgray");
            song.Update(user, sd, Service);
            var first = song.ToString();
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
