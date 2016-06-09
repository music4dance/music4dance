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

            var instance = DanceStatsManager.LoadFromJson(json,true);
            DanceStatsManager.SetInstance(instance);
            Assert.IsNotNull(instance);

            var jsonNew = DanceStatsManager.SaveToJson();
            Assert.IsNotNull(jsonNew);

            jsonNew = jsonNew.Replace("[ ]", "[]");
            Assert.AreEqual(json,jsonNew);
        }
    }
}
