using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using SongDatabase.Models;

namespace SDBTests
{
    [TestClass]
    public class SongContextTests
    {
        [TestMethod]
        public void TestTitleHash()
        {
            for (int i = 0; i < titles.Length; i++)
            {
                string t = titles[i];

                int hash = DanceMusicContext.CreateTitleHash(t);
                Assert.AreEqual<int>(hashes[i], hash);
            }
        }

        static string[] titles = new string[] {
            "ñ-é á",
            "Señor Bolero",
            "Viaje Tiemp Atrás",
            "Solo Tu",
            "Señales De Humo",
            "España Cañi",
            "Y'a Qu'les Filles Qui M'interessent",
            "A Namorádo",
            "Satisfaction (I Can't Get No)",
            "Moliendo Café"
        };

        static int[] hashes = new int[] {
            -2048828505,
            335748376,
            1113827306,
            2047398645,
            1119840081,
            637850480,
            -93783816,
            -46326465,
            1911801211,
            -1976465671
        };
    }
}

