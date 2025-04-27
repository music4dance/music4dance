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
            Assert.AreEqual(69, users.Count, "Count of Users");
            var dances = from d in service.Context.Dances select d;
            Assert.AreEqual(107, dances.Count(), "Count of Dances");
            var tts = from tt in service.Context.TagGroups select tt;
            Assert.AreEqual(494, tts.Count(), "Count of Tag Types");
            var searches = from ss in service.Context.Searches select ss;
            Assert.AreEqual(16, searches.Count(), "Count of Searches");
        }
    }
}
