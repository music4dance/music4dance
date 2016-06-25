using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class StatsTest
    {
        [TestMethod]
        public void LoadDanceStats()
        {
            var json = File.ReadAllText(@".\TestData\dancestatistics.txt");

            var instance = DanceStatsInstance.LoadFromJson(json,true);
            DanceStatsManager.SetInstance(instance);
            Assert.IsNotNull(instance);

            var jsonNew = instance.SaveToJson();
            Assert.IsNotNull(jsonNew);

            jsonNew = jsonNew.Replace("[ ]", "[]");
            jsonNew = jsonNew.Replace("\r\n", "\n");
            jsonNew = jsonNew.Replace("      \"SongTags\": \"\",\n", "");
            jsonNew = jsonNew.Replace("          \"SongTags\": \"\",\n", "");

            Assert.AreEqual(json,jsonNew);
        }
    }
}
