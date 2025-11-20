using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class KeywordQueryTests
    {
        [TestMethod]
        public void ShouldCorrectlyDetectLuceneTrue()
        {
            var searchQuery = new KeywordQuery("`test");
            Assert.IsTrue(searchQuery.IsLucene);
        }

        [TestMethod]
        public void ShouldCorrectlyDetectLuceneFalse()
        {
            var searchQuery = new KeywordQuery("test");
            Assert.IsFalse(searchQuery.IsLucene);
        }

        [TestMethod]
        public void ShouldReturnIsLuceneFalseOnEmptyString()
        {
            var searchQuery = new KeywordQuery("");
            Assert.IsFalse(searchQuery.IsLucene);
        }

        [TestMethod]
        public void ShouldReturnSearchTestForSimpleSyntax()
        {
            var searchQuery = new KeywordQuery("test");
            Assert.AreEqual("test", searchQuery.Keywords);
        }

        [TestMethod]
        public void ShouldReturnSearchTestForLuceneSyntax()
        {
            var searchQuery = new KeywordQuery("`test");
            Assert.AreEqual("test", searchQuery.Keywords);
        }

        [TestMethod]
        public void DescriptionIsAccurateForSimpleSearch()
        {
            var searchQuery = new KeywordQuery("test");
            Assert.AreEqual("containing the text \"test\"", searchQuery.Description);
        }

        [TestMethod]
        public void DescriptionIsAccurateForLuceneSearchWithoutFields()
        {
            var searchQuery = new KeywordQuery("`test");
            Assert.AreEqual("containing the text \"test\"", searchQuery.Description);
        }

        [TestMethod]
        public void DescriptionIsAccurateForSingleField()
        {
            var searchQuery = new KeywordQuery("`Title:(test)");
            Assert.AreEqual("where title contains \"test\"", searchQuery.Description);
        }

        [TestMethod]
        public void DescriptionIsAccurateForMultipleFields()
        {
            var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo)");
            Assert.AreEqual("where title contains \"test\" and artist contains \"foo\"", searchQuery.Description);
        }

        [TestMethod]
        public void DescriptionIsAccurateForMultipleFieldsAndAll()
        {
            var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo) bar");
            Assert.AreEqual("containing the text \"bar\" anywhere and title contains \"test\" and artist contains \"foo\"", searchQuery.Description);
        }

        [TestMethod]
        public void ShortDescriptionIsAccurateForSingleField()
        {
            var searchQuery = new KeywordQuery("`Title:(test)");
            Assert.AreEqual(@"""test"" in title", searchQuery.ShortDescription);
        }

        [TestMethod]
        public void ShortDescriptionIsAccurateForMultipleFields()
        {
            var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo)");
            Assert.AreEqual(@"""test"" in title and ""foo"" in artist", searchQuery.ShortDescription);
        }

        [TestMethod]
        public void ShortDescriptionIsAccurateForMultipleFieldsAndAll()
        {
            var searchQuery = new KeywordQuery("`Title:(test) Artist:(foo) bar");
            Assert.AreEqual(@"""bar"" anywhere and ""test"" in title and ""foo"" in artist", searchQuery.ShortDescription);
        }

        [TestMethod]
        public void ShouldAddPartWhenValueIsProvided()
        {
            var searchQuery = new KeywordQuery("something");
            var updatedQuery = searchQuery.Update("Title", "test");
            Assert.AreEqual("`Title:(test) something", updatedQuery.Query);
        }

        [TestMethod]
        public void ShouldDeletePartWhenValueIsNotProvided()
        {
            var searchQuery = new KeywordQuery("Title:(test) something");
            var updatedQuery = searchQuery.Update("Title", "");
            Assert.AreEqual("something", updatedQuery.Query);
        }
    }
}