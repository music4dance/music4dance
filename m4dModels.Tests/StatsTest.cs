using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class StatsTest
    {
        //[TestMethod]
        //public void LoadDanceStats()
        //{
        //    var json = File.ReadAllText(@".\TestData\dancestatistics.txt");

        //    var instance = DanceStatsInstance.LoadFromJson(json);
        //    DanceStatsManager.SetInstance(instance);
        //    Assert.IsNotNull(instance);

        //    var jsonNew = instance.SaveToJson();
        //    Assert.IsNotNull(jsonNew);

        //    jsonNew = jsonNew.Replace("[ ]", "[]");
        //    jsonNew = jsonNew.Replace("\r\n", "\n");
        //    const string strRegex = @"[ ]*""SongTags"": """",\n";
        //    var re = new Regex(strRegex, RegexOptions.Multiline);
        //    jsonNew = re.Replace(jsonNew, "");

        //    const string strRegex2 = @"[ ]*""SongTags"": """",\r\n";
        //    var re2 = new Regex(strRegex2, RegexOptions.Multiline);
        //    json = re2.Replace(json,"");

        //    Assert.AreEqual(json,jsonNew);
        //}
    }
}