using System.Net;

using m4d.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Utilities;

[TestClass]
public class MusicServiceRequestExceptionTests
{
    [TestMethod]
    public void CarriesTheStatusCode()
    {
        var e = new MusicServiceRequestException(HttpStatusCode.BadGateway, "Bad Gateway");
        Assert.AreEqual(HttpStatusCode.BadGateway, e.StatusCode);
        Assert.AreEqual("Bad Gateway", e.Message);
    }

    // Existing callers catch WebException for these, and PlaylistNotFoundException must keep
    // reaching them everywhere but the update batch
    [TestMethod]
    public void PlaylistNotFoundIsAMusicServiceRequestExceptionAndAWebException()
    {
        var inner = new MusicServiceRequestException(HttpStatusCode.NotFound, "Not Found");
        var e = new PlaylistNotFoundException("https://open.spotify.com/playlist/abc", inner);

        Assert.IsInstanceOfType<WebException>(e);
        Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);
        Assert.AreSame(inner, e.InnerException);
        StringAssert.Contains(e.Message, "playlist/abc");
    }
}
