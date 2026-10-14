#nullable disable

using m4d.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using System.Text;

namespace m4d.Tests.Services;

/// <summary>
/// License keys read as JWTs and the monitor that checks every expiring credential. The Spotify
/// service account's own checks are in ServiceAccountTests.
/// </summary>
[TestClass]
public class CredentialExpiryTests
{
    private const string ConfigKey = "Authentication:Test:Key";
    private static readonly DateTimeOffset Expires = new(2027, 9, 23, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ReadExpiry_ReadsTheExpClaim()
    {
        Assert.AreEqual(Expires, LicenseKeyCredential.ReadExpiry(Jwt(Expires)));
    }

    [TestMethod]
    public void ReadExpiry_ReadsAStringExpClaim()
    {
        // The shape Lucky Penny's AutoMapper keys actually use
        var payload = $$"""{"sub":"test","exp":"{{Expires.ToUnixTimeSeconds()}}"}""";

        Assert.AreEqual(Expires, LicenseKeyCredential.ReadExpiry(Jwt(payload)));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not-a-jwt")]
    public void ReadExpiry_NotAJwt_IsNull(string key)
    {
        Assert.IsNull(LicenseKeyCredential.ReadExpiry(key));
    }

    [TestMethod]
    public void ReadExpiry_NoExpClaim_IsNull()
    {
        Assert.IsNull(LicenseKeyCredential.ReadExpiry(Jwt((DateTimeOffset?)null)));
    }

    [TestMethod]
    public void Problem_Unset_IsQuiet()
    {
        Assert.IsNull(LicenseKeyCredential.Problem("Test key", ConfigKey, null, Expires));
    }

    [TestMethod]
    public void Problem_OutsideTheWindow_IsQuiet()
    {
        var now = Expires - LicenseKeyCredential.WarningWindow - TimeSpan.FromDays(1);

        Assert.IsNull(LicenseKeyCredential.Problem("Test key", ConfigKey, Jwt(Expires), now));
    }

    [TestMethod]
    public void Problem_InsideTheWindow_CountsTheDays()
    {
        var problem = LicenseKeyCredential.Problem(
            "Test key", ConfigKey, Jwt(Expires), Expires.AddDays(-10));

        Assert.AreEqual("The Test key expires on 2027-09-23 (in 10 days).", problem);
    }

    [TestMethod]
    public void Problem_Expired_SaysSo()
    {
        var problem = LicenseKeyCredential.Problem(
            "Test key", ConfigKey, Jwt(Expires), Expires.AddDays(1));

        Assert.AreEqual("The Test key expired on 2027-09-23.", problem);
    }

    [TestMethod]
    public void Problem_UnreadableKey_ReportsIt()
    {
        var problem = LicenseKeyCredential.Problem("Test key", ConfigKey, "garbage", Expires);

        StringAssert.Contains(problem, ConfigKey);
    }

    [TestMethod]
    public async Task Check_ReadsTheKeyFromConfiguration()
    {
        var credential = CreateCredential(Jwt(Expires));

        Assert.AreEqual(Expires, await credential.GetExpiry());
        StringAssert.Contains(await credential.Check(Expires.AddDays(-1)), "in 1 day)");
        Assert.IsNull(await credential.Check(Expires.AddYears(-1)));
    }

    [TestMethod]
    public async Task CheckAll_CollectsProblemsAndSurvivesAFailingCheck()
    {
        var monitor = new CredentialExpiryMonitor(
            [new ThrowingCredential(), CreateCredential(Jwt(Expires)), CreateCredential(null)],
            NullLogger<CredentialExpiryMonitor>.Instance);

        var problems = await monitor.CheckAll(Expires.AddDays(1));

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "expired");
    }

    private static LicenseKeyCredential CreateCredential(string key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [ConfigKey] = key })
            .Build();
        return new LicenseKeyCredential(
            "Test key", ConfigKey, "Renew it.", configuration, null, NullLogger.Instance);
    }

    // An unsigned JWT shaped like a Lucky Penny license key; only the payload matters here
    private static string Jwt(DateTimeOffset? expires) =>
        Jwt(expires == null
            ? """{"sub":"test"}"""
            : $$"""{"sub":"test","exp":{{expires.Value.ToUnixTimeSeconds()}}}""");

    private static string Jwt(string payload) =>
        $"{Base64Url("""{"alg":"none","typ":"JWT"}""")}.{Base64Url(payload)}.";

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ThrowingCredential : IExpiringCredential
    {
        public string Name => "Broken";
        public Task<DateTimeOffset?> GetExpiry() => throw new InvalidOperationException();
        public Task<string> Check(DateTimeOffset? now = null) => throw new InvalidOperationException();
    }
}
