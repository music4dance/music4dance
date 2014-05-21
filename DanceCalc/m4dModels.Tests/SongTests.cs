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
    }
}
