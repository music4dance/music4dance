using System.Net;

using m4d.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Utilities;

[TestClass]
public class MusicServiceRequestExceptionTests
{
    // Only a 404 means a playlist may be gone; MissingPlaylistPolicy must never see the others
    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, true)]
    [DataRow(HttpStatusCode.BadGateway, false)]
    [DataRow(HttpStatusCode.InternalServerError, false)]
    [DataRow(HttpStatusCode.TooManyRequests, false)]
    [DataRow(HttpStatusCode.Unauthorized, false)]
    [DataRow(HttpStatusCode.Forbidden, false)]
    public void IsNotFoundOnlyForA404(HttpStatusCode status, bool expected)
    {
        Assert.AreEqual(expected,
            MusicServiceRequestException.IsNotFound(new MusicServiceRequestException(status, "x")));
    }

    [TestMethod]
    public void OtherFailuresAreNotNotFound()
    {
        Assert.IsFalse(MusicServiceRequestException.IsNotFound(new WebException("Not Found")));
        Assert.IsFalse(MusicServiceRequestException.IsNotFound(new TaskCanceledException()));
        Assert.IsFalse(MusicServiceRequestException.IsNotFound(new HttpRequestException()));
    }

    // Existing callers catch WebException for these
    [TestMethod]
    public void IsAWebException()
    {
        Assert.IsInstanceOfType<WebException>(
            new MusicServiceRequestException(HttpStatusCode.NotFound, "Not Found"));
    }
}
