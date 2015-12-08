using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Web;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongFilterTests
    {
        // TODO: after spending the time writing these test it occurs to me that a more robust way of doing this would
        //  be to create a couple of static objects using json-like constructor syntax and go the other way, that way
        //  the tests would be resilient to changing the encoding scheme - so take the time to rework this if I ever
        //  change the scheme.
        [TestMethod]
        public void BasicFilter()
        {
            TestFilters(false);
        }


        [TestMethod]
        public void FilterWithEncoding()
        {
            TestFilters(true);
        }

        [TestMethod]
        public void FilterDescription()
        {
            var f1 = new SongFilter(@"Index-FXT-.-.-XI-.-100-120-1-+Instrumental:Music");
            var f2 = new SongFilter(@"Index-ALL-Dances-Funk-.-.-.-.-.-+Rock & Roll:Music|\-Jazz:Music|\-Pop:Music");
            var f3 = new SongFilter(@"Index-ALL-.-.--.-100-.-1");
            var f4 = new SongFilter(@"Index-ALL-Title-.--.-.-150-1");
            var f5 = new SongFilter(@"Advanced-.-.-.-.-+charlie|L-.-.-1");
            var f6 = new SongFilter(@"Advanced-.-.-.-.-\-charlie|");

            Trace.WriteLine(f1.Description);
            Trace.WriteLine(f2.Description);
            Trace.WriteLine(f3.Description);
            Trace.WriteLine(f4.Description);
            Trace.WriteLine(f5.Description);
            Trace.WriteLine(f6.Description);

            Assert.AreEqual(@"All Foxtrot songs available on Groove or ITunes, including tag Instrumental, having tempo between 100 and 120 beats per measure", f1.Description);
            Assert.AreEqual(@"All songs containing the text ""Funk"", including tag Rock & Roll, excluding tags Jazz or Pop", f2.Description);
            Assert.AreEqual(@"All songs having tempo greater than 100 beats per measure", f3.Description);
            Assert.AreEqual(@"All songs having tempo less than 150 beats per measure", f4.Description);
            Assert.AreEqual(@"All songs liked by charlie", f5.Description);
            Assert.AreEqual(@"All songs not edited by charlie", f6.Description);
        }

        [TestMethod]
        public void TestEmpty()
        {
            Assert.IsTrue(new SongFilter("@Index-ALL").IsEmpty);
            Assert.IsTrue(new SongFilter("@Index").IsEmpty);
            Assert.IsFalse(new SongFilter("@Index-ALL-Title-.--.-.-150-1").IsEmpty);
            Assert.IsTrue(new SongFilter("@Index-.-Dances-.-.-.-.-.-1").IsEmpty);
        }

        private static void TestFilters(bool withEncoding)
        {
            const string trivial = "{0}Trivial filter fails round-trip: {1}";
            var s = RoundTrip("", "", trivial, 1, false);
            RoundTrip(s, "", trivial, 1, withEncoding);

            const string simple = "{0}Simple filter fails round-trip: {1}";
            s = RoundTrip(F1, F1, simple, 1, false);
            RoundTrip(s, F1, simple, 1, withEncoding);

            const string complex = "{0}Complex filter fails round-trip: {1}";
            s = RoundTrip(F2, F2, complex, 1, false);
            RoundTrip(s, F2, complex, 2, withEncoding);
        }


        private static string RoundTrip(string fi, string f0, string message, int n, bool withEncoding)
        {
            var f = new SongFilter(fi);
            var s = f.ToString();

            // Round-trip http encoding to make sure that we preserve our values
            if (withEncoding)
            {
                var enc = HttpUtility.HtmlEncode(s);
                s = HttpUtility.HtmlDecode(enc);
            }
            Assert.AreEqual(s, f0, string.Format(message,withEncoding?"Encoded ":string.Empty,n));
            return s;
        }

        const string F1 = @"Index-SWG-Album-Goodman-X-.-50-150-1-%2BPop%3AMusic";
        const string F2 = @"Index-SWG-\\--\\-\\--X";
    }
}
