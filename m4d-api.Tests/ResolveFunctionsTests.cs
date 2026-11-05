using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using static m4dApi.Tests.FakeSearch;

namespace m4dApi.Tests;

[TestClass]
public class ResolveFunctionsTests
{
    private static HttpRequest Post(string body, string contentType = "application/json")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }

    private static Task<IActionResult> Resolve(FakeSearch search, HttpRequest request) =>
        new ResolveFunctions(search.Resolver, NullLogger<ResolveFunctions>.Instance).Resolve(request);

    [TestMethod]
    [DataRow("{not json", "application/json", "Request body is not valid JSON")]
    [DataRow("isrc=USWB19902945", "text/plain", "Content-Type must be application/json")]
    [DataRow("{\"title\":\"Blue Bayou\"}", "application/json", "Supply at least one id, or both title and artist")]
    [DataRow("null", "application/json", "Request body is required")]
    public async Task Resolve_RejectsBadRequestsWith400(string body, string contentType, string error)
    {
        var search = new FakeSearch();

        var result = await Resolve(search, Post(body, contentType));

        var badRequest = result as BadRequestObjectResult;
        Assert.IsNotNull(badRequest);
        StringAssert.Contains(JsonSerializer.Serialize(badRequest.Value), error);
        Assert.IsEmpty(search.Queries);
    }

    [TestMethod]
    public async Task Resolve_Returns503WithRetryAfterWhenSearchFails()
    {
        var request = Post("{\"isrc\":\"USWB19902945\"}");

        var result = await Resolve(new FakeSearch().Throws(503), request);

        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, (result as StatusCodeResult)?.StatusCode);
        Assert.AreEqual("5", request.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    [TestMethod]
    public async Task Resolve_ReturnsTheContractShape()
    {
        var search = new FakeSearch().Returns(
            Song("a", serviceIds: ["R:USWB19902945"], dances: ("WCS", 3, 120, ["Smooth"])));

        var result = await Resolve(search, Post("{\"isrc\":\"USWB19902945\"}"));

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        // The worker serializes with the web defaults: camelCase, and the attributes on the records
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, JsonSerializerOptions.Web));
        var root = json.RootElement;
        Assert.AreEqual("exact", root.GetProperty("match").GetString());
        Assert.AreEqual("isrc", root.GetProperty("method").GetString());
        Assert.IsFalse(root.TryGetProperty("queries", out _));
        var candidate = root.GetProperty("candidates")[0];
        Assert.IsFalse(candidate.TryGetProperty("evidence", out _));
        Assert.AreEqual("isrc", candidate.GetProperty("matchedBy")[0].GetString());
        Assert.AreEqual(231, candidate.GetProperty("lengthSeconds").GetInt32());
        Assert.AreEqual(120, candidate.GetProperty("beatsPerMinute").GetDouble());
        Assert.IsFalse(candidate.TryGetProperty("tempo", out _));
        var rating = candidate.GetProperty("danceRatings")[0];
        Assert.AreEqual(120, rating.GetProperty("beatsPerMinute").GetDouble());
        Assert.AreEqual("WCS", rating.GetProperty("danceId").GetString());
        Assert.AreEqual("West Coast Swing", rating.GetProperty("danceName").GetString());
        Assert.AreEqual("Smooth", rating.GetProperty("styles")[0].GetString());
    }

    [TestMethod]
    public async Task Resolve_IncludesEvidenceForFuzzyCandidates()
    {
        var search = new FakeSearch().Returns(Song("a", dances: ("WCS", 3, 120, null)));

        var result = await Resolve(search, Post("{\"title\":\"Blue Bayou\",\"artist\":\"Linda Ronstadt\"}"));

        var ok = (OkObjectResult)result;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, JsonSerializerOptions.Web));
        var candidate = json.RootElement.GetProperty("candidates")[0];
        Assert.IsFalse(candidate.TryGetProperty("matchedBy", out _));
        var evidence = candidate.GetProperty("evidence");
        Assert.AreEqual("high", evidence.GetProperty("confidence").GetString());
        Assert.AreEqual(JsonValueKind.Null, evidence.GetProperty("lengthDeltaSeconds").ValueKind);
    }
}
