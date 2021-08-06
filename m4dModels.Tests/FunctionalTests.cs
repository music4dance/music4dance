using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class FunctionalTests
    {
        [TestMethod]
        public async Task LoadDatabase()
        {
            using var service = await DanceMusicTester.CreatePopulatedService("LoadDatabase");

            var users = (from u in service.Context.Users select u)
                .ToList();
            Assert.AreEqual(69, users.Count(), "Count of Users");
            var dances = from d in service.Context.Dances select d;
            Assert.AreEqual(107, dances.Count(), "Count of Dances");
            var tts = from tt in service.Context.TagGroups select tt;
            Assert.AreEqual(494, tts.Count(), "Count of Tag Types");
            var searches = from ss in service.Context.Searches select ss;
            Assert.AreEqual(16, searches.Count(), "Count of Searches");
        }


        [TestMethod]
        public void PrettyLinkTest()
        {
            const string initial =
                "*East Coast Swing* is a standardized dance in [American Rhythm] style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes [Lindy Hop],  [Carolina Shag], [Balboa], [West Coast Swing], and [Jive].  \r\n\r\nThis dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.\r\n\r\nThe *East Coast Swing* is generally danced as the first dance of [American Rhythm] competitions.\r\n\r\nHustle is traditionally danced to [disco music](https://www.music4dance.net/song/addtags?tags=%2BDisco:Music)[Blues]";
            const string expected =
                @"*East Coast Swing* is a standardized dance in <a href='/dances/american-rhythm'>American Rhythm</a> style competition dancing as well as a social partner dance.  It is one of a number of different swing dances that developed concurrently with the swing style of jazz music in the mid twentieth century.  This group of dances also includes <a href='/dances/lindy-hop'>Lindy Hop</a>,  <a href='/dances/carolina-shag'>Carolina Shag</a>, <a href='/dances/balboa'>Balboa</a>, <a href='/dances/west-coast-swing'>West Coast Swing</a>, and <a href='/dances/jive'>Jive</a>.  

This dance may also be referred to as Eastern Swing, Triple Swing, Triple Step Swing, American Swing, or just Swing.

The *East Coast Swing* is generally danced as the first dance of <a href='/dances/american-rhythm'>American Rhythm</a> competitions.

Hustle is traditionally danced to [disco music](https://www.music4dance.net/song/addtags?tags=%2BDisco:Music)<a href='/dances/blues'>Blues</a>";

            var pretty = Dance.SmartLinks(initial);

            Trace.WriteLine(pretty);
            //for (int i = 0; i < expected.Length && i < pretty.Length; i++)
            //{
            //    if (expected[i] != pretty[i])
            //    {
            //        Trace.WriteLine(string.Format("{ 0}: '{1}' '{2}'",i,expected[i],pretty[i]));
            //    }
            //}
            Assert.AreEqual(expected, pretty);
        }
    }
}
