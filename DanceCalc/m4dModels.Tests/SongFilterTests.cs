using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using m4dModels;
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

        private void TestFilters(bool withEncoding)
        {
            string simple = "{0}Simple filter fails round-trip: {1}";
            string s = RoundTrip(_f1, _f1, simple, 1, false);
            RoundTrip(s, _f1, simple, 1, withEncoding);

            string complex = "{0}Complex filter fails round-trip: {1}";
            s = RoundTrip(_f2, _f2, complex, 1, false);
            RoundTrip(s, _f2, complex, 2, withEncoding);
        }

        private string RoundTrip(string fi, string f0, string message, int n, bool withEncoding)
        {
            SongFilter f = new SongFilter(fi);
            string s = f.ToString();

            // Round-trip http encoding to make sure that we preserve our values
            if (withEncoding)
            {
                string enc = HttpUtility.HtmlEncode(s);
                s = HttpUtility.HtmlDecode(enc);
            }
            Assert.AreEqual(s, f0, string.Format(message,withEncoding?"Encoded ":string.Empty,n));
            return s;
        }

        const string _f1 = @"Index-SWG-Album-Goodman-X-.-50-150-1-.";
        const string _f2 = @"Index-SWG-\\--\\-\\--X-.-.-.-1-.";
    }
}
