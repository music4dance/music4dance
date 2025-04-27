using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class UserQueryTest
    {
        [TestMethod]
        public void BasicUserQuery()
        {
            var a = new UserQuery();
            Assert.IsTrue(a.IsEmpty);
            Assert.IsFalse(a.HasOpinion);
            Assert.IsFalse(a.IsExclude);
            Assert.IsFalse(a.IsInclude);
            Assert.IsFalse(a.IsLike);
            Assert.IsFalse(a.IsHate);
            Assert.IsNull(a.UserName);

            a = new UserQuery("   ");
            Assert.IsTrue(a.IsEmpty);
            Assert.IsFalse(a.HasOpinion);
            Assert.IsFalse(a.IsExclude);
            Assert.IsFalse(a.IsInclude);
            Assert.IsFalse(a.IsLike);
            Assert.IsFalse(a.IsHate);
            Assert.IsNull(a.UserName);

            var b = new UserQuery("-DWGray|H");
            Assert.IsFalse(b.IsEmpty);
            Assert.IsTrue(b.HasOpinion);
            Assert.IsFalse(b.IsLike);
            Assert.IsTrue(b.IsHate);
            Assert.IsFalse(b.IsInclude);
            Assert.IsTrue(b.IsExclude);
            Assert.AreEqual("dwgray", b.UserName);

            var c = new UserQuery("dwgray");
            Assert.IsFalse(c.IsEmpty);
            Assert.IsFalse(c.HasOpinion);
            Assert.IsFalse(c.IsLike);
            Assert.IsFalse(c.IsHate);
            Assert.IsTrue(c.IsInclude);
            Assert.IsFalse(c.IsExclude);
            Assert.AreEqual("dwgray", c.UserName);
        }
    }
}
