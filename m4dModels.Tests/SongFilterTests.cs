using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Web;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongFilterTests
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            var t = DanceMusicTester.LoadDances().Result;
            Trace.WriteLine($"Loaded dances = {t}");
        }

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
            var f1 = new SongFilter(@"Index-FXT-.-.-I-.-100-120-1-+Instrumental:Music");
            var f2 = new SongFilter(
                @"Index-ALL-Dances-Funk-.-.-.-.-.-+Rock & Roll:Music|\-Jazz:Music|\-Pop:Music");
            var f3 = new SongFilter(@"Index-ALL-.-.--.-100-.-1");
            var f4 = new SongFilter(@"Index-ALL-Title-.--.-.-150-1");
            var f5 = new SongFilter(@"Advanced-.-.-.-.-+charlie|L-.-.-1");
            var f6 = new SongFilter(@"Advanced-.-.-.-.-\-charlie|");
            var f7 = new SongFilter(
                @"Advanced-.-.-.-.-null-.-.-1-+R&B / Soul:Music|+Rhythm and Blues:Music|+Blues:Music|");
            var f8 = new SongFilter(
                @"Advanced-SLS-.-.-S-null-.-.-1-|\-Christian / Gospel:Music|\-TV Theme Song:Music|\-Doo Wop:Music");
            var f9 = new SongFilter(@"Advanced-MBO,RMB,SMB-.-.-.-null-.-.-1-|");
            var f10 = new SongFilter(@"Advanced-AND,ECS,FXT,TGO-.-.-A-null-.-.-1-|");
            var f11 = new SongFilter(@"Advanced-RMB-Tempo-.-A-null-.-.-1-|");
            var f12 = new SongFilter(@"Advanced-AND,RMB,BCH-Created_desc-.-.-null-.-180-1-|");

            Trace.WriteLine(f1.Description);
            Trace.WriteLine(f2.Description);
            Trace.WriteLine(f3.Description);
            Trace.WriteLine(f4.Description);
            Trace.WriteLine(f5.Description);
            Trace.WriteLine(f6.Description);
            Trace.WriteLine(f7.Description);
            Trace.WriteLine(f8.Description);
            Trace.WriteLine(f9.Description);
            Trace.WriteLine(f10.Description);
            Trace.WriteLine(f11.Description);
            Trace.WriteLine(f12.Description);

            Assert.AreEqual(
                @"All Foxtrot songs available on ITunes, including tag Instrumental, having tempo between 100 and 120 beats per minute. Sorted by Dance Rating from most popular to least popular.",
                f1.Description);
            Assert.AreEqual(
                @"All songs containing the text ""Funk"", including tag Rock & Roll, excluding tags Jazz or Pop. Sorted by Dance Rating from most popular to least popular.",
                f2.Description);
            Assert.AreEqual(
                @"All songs having tempo greater than 100 beats per minute. Sorted by Dance Rating from most popular to least popular.",
                f3.Description);
            Assert.AreEqual(
                @"All songs having tempo less than 150 beats per minute. Sorted by Title from A to Z.",
                f4.Description);
            Assert.AreEqual(@"All songs liked by charlie. Sorted by Dance Rating from most popular to least popular.", f5.Description);
            Assert.AreEqual(@"All songs not edited by charlie. Sorted by Dance Rating from most popular to least popular.", f6.Description);
            Assert.AreEqual(
                @"All songs including tags Blues, R&B / Soul and Rhythm and Blues. Sorted by Dance Rating from most popular to least popular.",
                f7.Description);
            Assert.AreEqual(
                @"All Salsa songs available on Spotify, excluding tags Christian / Gospel, Doo Wop or TV Theme Song. Sorted by Dance Rating from most popular to least popular.",
                f8.Description);
            Assert.AreEqual(
                @"All songs danceable to any of Mambo, Rumba or Samba. Sorted by Dance Rating from most popular to least popular.",
                f9.Description);
            Assert.AreEqual(
                @"All songs danceable to all of East Coast Swing, Foxtrot and Tango (Ballroom) available on Amazon. Sorted by Dance Rating from most popular to least popular.",
                f10.Description);
            Assert.AreEqual(
                @"All Rumba songs available on Amazon. Sorted by Tempo from slowest to fastest.",
                f11.Description);
            Assert.AreEqual(
                @"All songs danceable to all of Rumba and Bachata having tempo less than 180 beats per minute. Sorted by When Added from oldest to newest.",
                f12.Description);
        }

        [TestMethod]
        public void FilterDescriptionV2()
        {
            var f1 = new SongFilter(F1V2);
            var f2 = new SongFilter(F2V2);

            Trace.WriteLine(f1.Description);
            Trace.WriteLine(f2.Description);

            Assert.AreEqual(
                @"All Swing songs containing the text ""Goodman"", available on ITunes, including tag Pop, having tempo between 50 and 150 beats per minute, having length between 30 and 90 seconds. Sorted by Dance Rating from most popular to least popular.",
                f1.Description);
            Assert.AreEqual(
                @"All Swing songs available on ITunes, having length between 30 and 90 seconds. Sorted by Dance Rating from most popular to least popular.",
                f2.Description);
        }

        [TestMethod]
        public void TestEmpty()
        {
            Assert.IsTrue(new SongFilter(@"Index-ALL").IsEmpty);
            Assert.IsTrue(new SongFilter(@"Index").IsEmpty);
            Assert.IsFalse(new SongFilter(@"Index-ALL-Title-.--.-.-150-1").IsEmpty);
            Assert.IsTrue(new SongFilter(@"Index-.-Dances-.-.-.-.-.-1").IsEmpty);
            Assert.IsFalse(new SongFilter(@"v2-Index-.-Dances-.-.-.-.-.-1").IsEmpty);
            Assert.IsTrue(new SongFilter(@"v2-Index-.-Dances-.-.-.-.-.-.-.-1").IsEmpty);
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

            const string simple2 = "{0}Simple v2 filter fails round-trip: {1}";
            s = RoundTrip(F1V2, F1V2, simple2, 1, false);
            RoundTrip(s, F1V2, simple2, 1, withEncoding);

            const string complex2 = "{0}Complex v2 filter fails round-trip: {1}";
            s = RoundTrip(F2V2, F2V2, complex2, 1, false);
            RoundTrip(s, F2V2, complex2, 2, withEncoding);
        }

        private static void TestV2Filters(bool withEncoding)
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


        private static string RoundTrip(string fi, string f0, string message, int n,
            bool withEncoding)
        {
            var f = new SongFilter(fi);
            var s = f.ToString();

            // Round-trip http encoding to make sure that we preserve our values
            if (withEncoding)
            {
                var enc = HttpUtility.HtmlEncode(s);
                s = HttpUtility.HtmlDecode(enc);
            }

            Assert.AreEqual(
                f0, s,
                string.Format(message, withEncoding ? "Encoded " : string.Empty, n));
            return s;
        }

        private const string F1 = @"Index-SWG-Album-Goodman-X-.-50-150-1-+Pop:Music";
        private const string F2 = @"Index-SWG-.-.-I";
        private const string F1V2 = @"v2-Index-SWG-Album-Goodman-I-.-50-150-30-90-1-+Pop:Music";
        private const string F2V2= @"v2-Index-SWG-.-.-I-.-.-.-30-90";
    }
}
