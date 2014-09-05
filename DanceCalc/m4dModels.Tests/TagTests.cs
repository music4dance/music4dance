using System;
using m4dModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagTests
    {
        string _batch = "Swing|Blues|Country";
        string _result = "SongId={00000000-0000-0000-0000-000000000000}	User=dwgray	Title=A Song (With a subtitle)	Artist=Crazy Artist	Tag=Swing	Tag=Blues	Tag=Country";
        [TestMethod]
        public void AddTags()
        {
            SongDetails sd = CreateSong();

            sd.AddTag("Swing");
            sd.AddTag("Blues");
            sd.AddTag("Country");

            Assert.AreEqual(sd.TagSummary, _batch, "Adding to SongDetails Failed");

            Song s = new Song();
            ApplicationUser user = s_users.FindUser("dwgray");
            s.Create(sd, user, Song.CreateCommand, null, s_factories, s_users);

            // TODO: Probalby want to abstract this out...
            string result = s.ToString();
            Regex rgx = new Regex("\tTime=[^\t]*");
            result = rgx.Replace(result, "");
            Trace.WriteLine(result);

            Assert.AreEqual(result,_result,"Creating Song");
        }

        [TestMethod]
        public void BatchAddTags()
        {
            SongDetails sd = CreateSong();

            sd.AddTags(_batch);

            Assert.AreEqual(sd.TagSummary, _batch, "Adding to SongDetails Failed");
        }

        [TestMethod]
        public void TagCategories()
        {
            Song song = new Song();
            Tag t = s_factories.CreateTag(song, "Swing");
            TagType tt = t.Type;

            Assert.AreEqual(tt.CategoryList[0], "Dance");
            Assert.AreEqual(tt.CategoryList[1], "Genre");
        }

        private SongDetails CreateSong()
        {
            return new SongDetails { Title = "A Song (With a subtitle)", Artist = "Crazy Artist" };
        }

        static MockUserMap s_users = new MockUserMap();
        static MockFactories s_factories = new MockFactories();
    }
}
