using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagFormatTests
    {
        [TestMethod]
        public void EncodeTags()
        {
            for (var i = 0; i < Decoded.Length; i++)
            {
                var d = Decoded[i];
                var e = TagGroup.TagEncode(d);
                Trace.WriteLine(e);
                Assert.AreEqual(Encoded[i], e);
            }
        }

        [TestMethod]
        public void DecodeTags()
        {
            for (var i = 0; i < Encoded.Length; i++)
            {
                var e = Encoded[i];
                var d = TagGroup.TagDecode(e);
                Assert.AreEqual(Decoded[i], d);
            }
        }


        private static readonly string[] Decoded = new string[]
        {
            "Thé!üt + (Avíañ):Ñúgg--t",
            "Blues / Folk:Music",
            "Christian & Gospel:Music",
            "contemporary-rhythm-and-blues:Music"
        };

        private static readonly string[] Encoded = new string[]
        {
            "Thé-21üt-w-2b-w-28Avíañ-29-pÑúgg----t",
            "Blues-w-s-wFolk-pMusic",
            "Christian-w-m-wGospel-pMusic",
            "contemporary--rhythm--and--blues-pMusic"
        };
    }
}