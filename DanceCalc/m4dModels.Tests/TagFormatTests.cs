using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace m4dModels.Tests
{
    [TestClass]
    public class TagFormatTests
    {
        [TestMethod]
        public void EncodeTags()
        {
            for (int i = 0; i < Decoded.Length; i++ )
            {
                string d = Decoded[i];
                string e = TagType.TagEncode(d);
                Trace.WriteLine(e);
                Assert.AreEqual(Encoded[i], e);
            }
        }

        [TestMethod]
        public void DecodeTags()
        {
            for (int i = 0; i < Encoded.Length; i++)
            {
                string e = Encoded[i];
                string d = TagType.TagDecode(e);
                Assert.AreEqual(Decoded[i], d);
            }
        }


        static readonly string[] Decoded = new string[]
        {
            "Thé!üt + (Avíañ):Ñúgg--t",
            "Blues / Folk:Music",
            "Christian & Gospel:Music",
            "contemporary-rhythm-and-blues:Music",
        };

        static readonly string[] Encoded = new string[]
        {
            "Thé-21üt-w-2b-w-28Avíañ-29-pÑúgg----t",
            "Blues-w-s-wFolk-pMusic",
            "Christian-w-m-wGospel-pMusic",
            "contemporary--rhythm--and--blues-pMusic",
        };

    }
}
