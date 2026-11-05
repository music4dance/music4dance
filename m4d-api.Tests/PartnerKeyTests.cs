using Microsoft.Extensions.Logging.Abstractions;

namespace m4dApi.Tests;

[TestClass]
public class PartnerKeyFormatTests
{
    [TestMethod]
    public void Generate_MakesAKeyThatParsesToItsKeyId()
    {
        var key = PartnerKeyFormat.Generate(PartnerKeyFormat.LivePrefix, out var keyId);

        StringAssert.StartsWith(key, $"m4d_live_{keyId}_");
        Assert.AreEqual(12, keyId.Length);
        Assert.AreEqual(9 + 12 + 1 + 43, key.Length);
        Assert.IsTrue(PartnerKeyFormat.TryParse(key, out var parsed));
        Assert.AreEqual(keyId, parsed);
    }

    [TestMethod]
    public void Generate_MakesDifferentKeys()
    {
        var keys = Enumerable.Range(0, 100)
            .Select(i => PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out _))
            .ToHashSet();

        Assert.HasCount(100, keys);
    }

    [TestMethod]
    public void Generate_RejectsAnUnknownPrefix() =>
        Assert.ThrowsExactly<ArgumentException>(() => PartnerKeyFormat.Generate("m4d_prod_", out _));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("m4d_live_")]
    [DataRow("m4d_prod_7KQ2M9XA4PZT_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ")]
    [DataRow("m4d_live_7KQ2M9XA4PZ_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ")]
    [DataRow("m4d_live_7KQ2M9XA4PZO_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ")]
    [DataRow("m4d_live_7KQ2M9XA4PZT_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOP")]
    [DataRow("m4d_live_7KQ2M9XA4PZT_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOP=")]
    [DataRow(" m4d_live_7KQ2M9XA4PZT_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ")]
    public void TryParse_RejectsMalformedKeys(string key) =>
        Assert.IsFalse(PartnerKeyFormat.TryParse(key, out _));

    [TestMethod]
    public void TryParse_AcceptsUnderscoresInTheSecret()
    {
        Assert.IsTrue(PartnerKeyFormat.TryParse("m4d_test_7KQ2M9XA4PZT_a_c-efghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ", out var keyId));
        Assert.AreEqual("7KQ2M9XA4PZT", keyId);
    }

    [TestMethod]
    public void Matches_ComparesAgainstTheHash()
    {
        var key = PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out _);
        var other = PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out _);
        var hash = PartnerKeyFormat.Hash(key);

        Assert.AreEqual(64, hash.Length);
        Assert.IsTrue(PartnerKeyFormat.Matches(key, hash));
        Assert.IsFalse(PartnerKeyFormat.Matches(other, hash));
        Assert.IsFalse(PartnerKeyFormat.Matches(key, "not hex"));
        Assert.IsFalse(PartnerKeyFormat.Matches(key, new string('z', 64)));
        Assert.IsFalse(PartnerKeyFormat.Matches(key, null));
    }

    [TestMethod]
    [DataRow("test-partner", true)]
    [DataRow("p1", true)]
    [DataRow("a", false)]
    [DataRow("Partner", false)]
    [DataRow("-partner", false)]
    [DataRow("partner_one", false)]
    [DataRow(null, false)]
    public void IsValidClientId(string clientId, bool valid) =>
        Assert.AreEqual(valid, PartnerKeyFormat.IsValidClientId(clientId));

    [TestMethod]
    public void ClientConfig_RoundTripsAndLeavesOutDefaults()
    {
        var json = new ClientConfig("Partner", DailyCap: 500).ToJson();

        Assert.AreEqual("{\"name\":\"Partner\",\"dailyCap\":500,\"status\":\"active\"}", json);
        Assert.AreEqual(new ClientConfig("Partner", DailyCap: 500), ClientConfig.FromJson(json));
        Assert.IsFalse(new ClientConfig("Partner", Status: "Disabled").IsActive);
    }
}

[TestClass]
public class PartnerKeyStoreTests
{
    [TestMethod]
    public void Authenticate_IsUnavailableUntilTheFirstLoad()
    {
        var partners = new FakePartners(load: false);

        Assert.IsFalse(partners.Store.Loaded);
        Assert.AreEqual(AuthStatus.Unavailable, partners.Store.Authenticate(partners.Key).Status);
    }

    [TestMethod]
    public void Authenticate_AcceptsAKnownKeyWithDefaultLimits()
    {
        var partners = new FakePartners();

        var result = partners.Store.Authenticate(partners.Key);

        Assert.AreEqual(AuthStatus.Ok, result.Status);
        Assert.AreEqual(FakePartners.ClientId, result.ClientId);
        Assert.IsNull(result.Client.RatePerSecond);
    }

    [TestMethod]
    public void Authenticate_RejectsUnknownAndTamperedKeys()
    {
        var partners = new FakePartners();
        var unknown = PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out _);
        // Same keyId, different secret
        var tampered = partners.Key[..^1] + (partners.Key[^1] == 'A' ? 'B' : 'A');

        Assert.AreEqual(AuthStatus.Unauthorized, partners.Store.Authenticate(unknown).Status);
        Assert.AreEqual(AuthStatus.Unauthorized, partners.Store.Authenticate(tampered).Status);
        Assert.AreEqual(AuthStatus.Unauthorized, partners.Store.Authenticate("junk").Status);
    }

    [TestMethod]
    public void Authenticate_ReturnsForbiddenForADisabledClient()
    {
        var partners = new FakePartners(new ClientConfig("Partner", Status: ClientConfig.Disabled));

        Assert.AreEqual(AuthStatus.Forbidden, partners.Store.Authenticate(partners.Key).Status);
    }

    [TestMethod]
    public async Task Refresh_DropsRevokedAndExpiredKeys()
    {
        var partners = new FakePartners();
        var expiring = PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out _);
        partners.Vault.AddKey(expiring, FakePartners.ClientId, expires: partners.Time.GetUtcNow().AddMinutes(1));
        await partners.Store.Refresh(CancellationToken.None);
        Assert.AreEqual(AuthStatus.Ok, partners.Store.Authenticate(expiring).Status);

        // Revoking disables the key's secret; the store doesn't try to read it
        partners.Vault.AddKey(partners.Key, FakePartners.ClientId, enabled: false);
        partners.Vault.Reads.Clear();
        await partners.Store.Refresh(CancellationToken.None);
        partners.Time.Advance(TimeSpan.FromMinutes(2));

        Assert.AreEqual(AuthStatus.Unauthorized, partners.Store.Authenticate(partners.Key).Status);
        Assert.AreEqual(AuthStatus.Unauthorized, partners.Store.Authenticate(expiring).Status);
        Assert.IsEmpty(partners.Vault.Reads);
    }

    [TestMethod]
    public async Task Refresh_ReadsOnlyNewOrChangedSecrets()
    {
        var partners = new FakePartners(new ClientConfig("Partner"));
        partners.Vault.Reads.Clear();

        await partners.Store.Refresh(CancellationToken.None);
        Assert.IsEmpty(partners.Vault.Reads);

        partners.Vault.AddClient(FakePartners.ClientId, new ClientConfig("Partner", DailyCap: 10));
        await partners.Store.Refresh(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "client-test-partner" }, partners.Vault.Reads);
        Assert.AreEqual(10, partners.Store.Authenticate(partners.Key).Client.DailyCap);
    }

    [TestMethod]
    public async Task Refresh_KeepsTheCacheWhenTheVaultFails()
    {
        var partners = new FakePartners();
        partners.Vault.Fail = true;

        await Assert.ThrowsExactlyAsync<Azure.RequestFailedException>(
            () => partners.Store.Refresh(CancellationToken.None));

        Assert.AreEqual(AuthStatus.Ok, partners.Store.Authenticate(partners.Key).Status);
    }

    [TestMethod]
    public async Task Refresh_KeepsTheOldConfigWhenANewOneIsMalformed()
    {
        var partners = new FakePartners(new ClientConfig("Partner", DailyCap: 10));
        partners.Vault.Set(PartnerKeyFormat.ClientSecretName(FakePartners.ClientId), "{not json");

        await partners.Store.Refresh(CancellationToken.None);

        Assert.AreEqual(10, partners.Store.Authenticate(partners.Key).Client.DailyCap);
    }

    [TestMethod]
    public async Task Refresh_IgnoresAKeyWithNoClientTagAndOtherSecrets()
    {
        var partners = new FakePartners();
        var untagged = PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out var keyId);
        partners.Vault.Set(PartnerKeyFormat.KeySecretName(keyId), PartnerKeyFormat.Hash(untagged));
        partners.Vault.Set("something-else", "value");

        await partners.Store.Refresh(CancellationToken.None);

        Assert.AreEqual(AuthStatus.Unauthorized, partners.Store.Authenticate(untagged).Status);
        Assert.AreEqual(1, partners.Store.KeyCount);
    }
}

[TestClass]
public class ClientRateLimiterTests
{
    [TestMethod]
    public void TryAcquire_AllowsABurstThenRefillsAtTheRate()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var limiter = new ClientRateLimiter(time);

        for (var i = 0; i < 3; i++)
        {
            Assert.IsTrue(limiter.TryAcquire("a", 2, 3, out _));
        }

        Assert.IsFalse(limiter.TryAcquire("a", 2, 3, out var retryAfter));
        Assert.AreEqual(0.5, retryAfter.TotalSeconds, 0.001);
        // Other clients have their own bucket
        Assert.IsTrue(limiter.TryAcquire("b", 2, 3, out _));

        time.Advance(TimeSpan.FromSeconds(0.5));
        Assert.IsTrue(limiter.TryAcquire("a", 2, 3, out _));
        Assert.IsFalse(limiter.TryAcquire("a", 2, 3, out _));

        // Never more than the burst, however long it's idle
        time.Advance(TimeSpan.FromHours(1));
        Assert.AreEqual(3, Enumerable.Range(0, 10).Count(i => limiter.TryAcquire("a", 2, 3, out _)));
    }
}

[TestClass]
public class UsageMeterTests
{
    [TestMethod]
    public async Task Flush_AddsCountsToTheTableAndKeepsTheTotal()
    {
        var partners = new FakePartners();
        partners.Usage.Record("a", "exact");
        partners.Usage.Record("a", "exact");
        partners.Usage.Record("a", UsageMeter.Limited);
        Assert.AreEqual(2, partners.Usage.DailyTotal("a"));

        await partners.Usage.Flush(CancellationToken.None);

        var row = partners.Table.Rows[("a", "2026-11-04")];
        Assert.AreEqual(2, row["exact"]);
        Assert.AreEqual(1, row[UsageMeter.Limited]);
        Assert.AreEqual(2, row[StorageUsageTable.TotalColumn]);
        Assert.AreEqual(2, partners.Usage.DailyTotal("a"));
    }

    [TestMethod]
    public async Task Flush_PicksUpOtherInstancesCounts()
    {
        var partners = new FakePartners();
        var other = new UsageMeter(partners.Table, NullLogger<UsageMeter>.Instance, partners.Time);
        partners.Usage.Record("a", "exact");
        await partners.Usage.Flush(CancellationToken.None);

        other.Record("a", "fuzzy");
        other.Record("a", "none");
        await other.Flush(CancellationToken.None);
        await partners.Usage.Flush(CancellationToken.None);

        Assert.AreEqual(3, partners.Usage.DailyTotal("a"));
    }

    [TestMethod]
    public async Task Flush_KeepsCountsWhenStorageFails()
    {
        var partners = new FakePartners();
        partners.Usage.Record("a", "exact");
        partners.Table.Fail = true;

        await partners.Usage.Flush(CancellationToken.None);
        Assert.AreEqual(1, partners.Usage.DailyTotal("a"));

        partners.Table.Fail = false;
        await partners.Usage.Flush(CancellationToken.None);
        Assert.AreEqual(1, partners.Table.Rows[("a", "2026-11-04")]["exact"]);
    }

    [TestMethod]
    public async Task DailyTotal_StartsOverAtUtcMidnight()
    {
        var partners = new FakePartners();
        partners.Usage.Record("a", "exact");
        await partners.Usage.Flush(CancellationToken.None);

        partners.Time.Advance(TimeSpan.FromHours(4));
        await partners.Usage.Flush(CancellationToken.None);

        Assert.AreEqual(0, partners.Usage.DailyTotal("a"));
    }
}
