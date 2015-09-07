using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class MusicServiceTests
    {
        [TestMethod]
        public void GetPurchaseLink()
        {
            CheckLink('A', "D:B003CJ88U0", "D:B003CJ0MXQ",@"http://www.amazon.com/gp/product/B003CJ0MXQ/ref=as_li_ss_tl?ie=UTF8&camp=1789&creative=390957&creativeASIN=B003CJ0MXQ&linkCode=as2&tag=music4dance-20");
            CheckLink('I', "201509007", "201509785",@"http://itunes.apple.com/album/id201509007?i=201509785&uo=4&at=11lwtf");
            CheckLink('S', "7kIi4z3UO8ZqH3GVX18p7h", "5gfPJ45gpn3ThswDyeW0Qc[EN,CA]", @"http://open.spotify.com/track/5gfPJ45gpn3ThswDyeW0Qc",new []{"EN","CA"});
            CheckLink('X', null, "music.5F058400-0100-11DB-89CA-0019B92A3933", @"http://music.xbox.com/Track/5F058400-0100-11DB-89CA-0019B92A3933?partnerID=Music4Dance&action=play&target=app");
        }

        private static void CheckLink(char cid, string album, string song, string expected, string[] regions = null)
        {
            var link = MusicService.GetService(cid).GetPurchaseLink(PurchaseType.Song, album, song);

            //Trace.WriteLine(link.Link);

            Assert.AreEqual(expected,link.Link);
            if (regions == null)
            {
                Assert.IsNull(link.AvailableMarkets);
            }
            else
            {
                Assert.IsNotNull(link.AvailableMarkets);
                Assert.AreEqual(regions.Length, link.AvailableMarkets.Length);
                for (var i = 0; i < regions.Length; i++)
                {
                    Assert.AreEqual(regions[i], link.AvailableMarkets[i]);
                }
            }
        }

        [TestMethod]
        public void TestFilter()
        {
            Assert.IsNull(MusicService.FormatPurchaseFilter(""));
            Assert.IsNull(MusicService.FormatPurchaseFilter("BZY"));
            Assert.AreEqual("Amazon, Spotify",MusicService.FormatPurchaseFilter("AS"));
            Assert.AreEqual("ITunes; Groove", MusicService.FormatPurchaseFilter("IX","; "));
        }
    }
}






