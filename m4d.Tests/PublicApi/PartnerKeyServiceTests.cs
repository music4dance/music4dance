using m4d.PublicApi;

using m4dApi;

using m4dModels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace m4d.Tests.PublicApi;

[TestClass]
public class PartnerKeyServiceTests
{
    private sealed class FakeVault(bool connected = true) : IPartnerKeyVault
    {
        public Dictionary<string, (string Hash, string Client, string Label, bool Enabled)> Keys { get; } = [];
        public Dictionary<string, ClientConfig> Clients { get; } = [];
        public bool FailNextWrite { get; set; }

        public bool IsConnected => connected;
        public string VaultName => "m4d-api-keys-test";

        public Task SetKey(string keyId, string hash, string clientId, string label)
        {
            if (FailNextWrite)
            {
                FailNextWrite = false;
                throw new Azure.RequestFailedException(503, "vault down");
            }

            if (connected)
            {
                Keys[keyId] = (hash, clientId, label, true);
            }
            return Task.CompletedTask;
        }

        public Task DisableKey(string keyId)
        {
            if (Keys.TryGetValue(keyId, out var key))
            {
                Keys[keyId] = key with { Enabled = false };
            }

            return Task.CompletedTask;
        }

        public Task SetClient(string clientId, ClientConfig config)
        {
            Clients[clientId] = config;
            return Task.CompletedTask;
        }
    }

    private static DanceMusicContext NewContext() =>
        new(new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase($"partner-keys-{Guid.NewGuid()}")
            .Options);

    private static IConfiguration Configuration(string environment = "test")
    {
        var configuration = new ConfigurationManager();
        configuration["PartnerApi:Environment"] = environment;
        return configuration;
    }

    private static async Task<(PartnerKeyService Service, FakeVault Vault, DanceMusicContext Context)> WithClient(
        bool connected = true, string environment = "test")
    {
        var context = NewContext();
        var vault = new FakeVault(connected);
        var service = new PartnerKeyService(context, vault, Configuration(environment));
        _ = await service.SaveClient("acme", "Acme Dance", 5, 20, 1000, false, create: true);
        return (service, vault, context);
    }

    [TestMethod]
    public async Task Issue_StoresTheHashInTheVaultAndOnlyMetadataInSql()
    {
        var (service, vault, context) = await WithClient();

        var issued = await service.Issue("acme", " backend ", "dev1");

        StringAssert.StartsWith(issued.Key, PartnerKeyFormat.TestPrefix);
        Assert.IsTrue(PartnerKeyFormat.TryParse(issued.Key, out var keyId));
        Assert.AreEqual(keyId, issued.KeyId);
        Assert.IsNull(issued.ManualCommand);

        var stored = vault.Keys[keyId];
        Assert.IsTrue(PartnerKeyFormat.Matches(issued.Key, stored.Hash));
        Assert.AreEqual("acme", stored.Client);
        Assert.AreEqual("backend", stored.Label);

        var row = await context.PartnerApiKeys.SingleAsync();
        Assert.AreEqual(keyId, row.KeyId);
        Assert.AreEqual("backend", row.Label);
        Assert.AreEqual("dev1", row.CreatedBy);
        Assert.IsTrue(row.IsActive);
    }

    [TestMethod]
    public async Task Issue_UsesLiveKeysOnlyWhenConfigured()
    {
        var (service, _, _) = await WithClient(environment: "live");

        var issued = await service.Issue("acme", null, "dev1");

        StringAssert.StartsWith(issued.Key, PartnerKeyFormat.LivePrefix);
    }

    [TestMethod]
    public async Task Issue_AllowsTwoActiveKeysForRotation()
    {
        var (service, _, _) = await WithClient();
        var first = await service.Issue("acme", "old", "dev1");
        _ = await service.Issue("acme", "new", "dev1");

        var ex = await Assert.ThrowsExactlyAsync<PartnerKeyException>(() => service.Issue("acme", "third", "dev1"));
        StringAssert.Contains(ex.Message, "at most 2 active keys");

        _ = await service.Revoke(first.KeyId, "dev1");
        _ = await service.Issue("acme", "third", "dev1");
    }

    [TestMethod]
    public async Task Issue_RefusesADisabledOrUnknownClient()
    {
        var (service, _, _) = await WithClient();
        _ = await service.SaveClient("acme", "Acme Dance", null, null, null, disabled: true, create: false);

        _ = await Assert.ThrowsExactlyAsync<PartnerKeyException>(() => service.Issue("acme", null, "dev1"));
        _ = await Assert.ThrowsExactlyAsync<PartnerKeyException>(() => service.Issue("nobody", null, "dev1"));
    }

    [TestMethod]
    public async Task Issue_RecordsNothingWhenTheVaultWriteFails()
    {
        var (service, vault, context) = await WithClient();
        vault.FailNextWrite = true;

        _ = await Assert.ThrowsExactlyAsync<Azure.RequestFailedException>(() => service.Issue("acme", null, "dev1"));

        Assert.IsEmpty(vault.Keys);
        Assert.AreEqual(0, await context.PartnerApiKeys.CountAsync());
    }

    [TestMethod]
    public async Task Issue_GivesTheManualCommandWithoutAVault()
    {
        var (service, vault, context) = await WithClient(connected: false);

        var issued = await service.Issue("acme", "backend", "dev1");

        Assert.IsEmpty(vault.Keys);
        Assert.AreEqual(1, await context.PartnerApiKeys.CountAsync());
        StringAssert.Contains(issued.ManualCommand, $"--name key-{issued.KeyId} --value {PartnerKeyFormat.Hash(issued.Key)}");
        StringAssert.Contains(issued.ManualCommand, "--tags client=acme \"label=backend\"");
        Assert.DoesNotContain(issued.Key, issued.ManualCommand);
    }

    [TestMethod]
    public async Task Revoke_DisablesTheVaultSecretAndRecordsWho()
    {
        var (service, vault, context) = await WithClient();
        var issued = await service.Issue("acme", null, "dev1");

        var revoked = await service.Revoke(issued.KeyId, "admin");

        Assert.IsFalse(vault.Keys[issued.KeyId].Enabled);
        Assert.IsFalse(revoked.IsActive);
        Assert.AreEqual("admin", (await context.PartnerApiKeys.SingleAsync()).RevokedBy);
        _ = await Assert.ThrowsExactlyAsync<PartnerKeyException>(() => service.Revoke("NOSUCHKEY234", "admin"));
    }

    [TestMethod]
    public async Task SaveClient_WritesLimitsToTheVault()
    {
        var (service, vault, _) = await WithClient();

        Assert.AreEqual(new ClientConfig("Acme Dance", 5, 20, 1000), vault.Clients["acme"]);

        _ = await service.SaveClient("acme", "Acme", null, null, 50, disabled: true, create: false);

        Assert.AreEqual(new ClientConfig("Acme", null, null, 50, ClientConfig.Disabled), vault.Clients["acme"]);
    }

    [TestMethod]
    [DataRow("A", "Name", "client id")]
    [DataRow("Bad_Id", "Name", "client id")]
    [DataRow("acme2", "", "name is required")]
    public async Task SaveClient_ValidatesInput(string id, string name, string error)
    {
        var (service, _, _) = await WithClient();

        var ex = await Assert.ThrowsExactlyAsync<PartnerKeyException>(
            () => service.SaveClient(id, name, null, null, null, false, create: true));

        StringAssert.Contains(ex.Message, error);
    }

    [TestMethod]
    public async Task SaveClient_RefusesDuplicatesAndBadLimits()
    {
        var (service, _, _) = await WithClient();

        _ = await Assert.ThrowsExactlyAsync<PartnerKeyException>(
            () => service.SaveClient("acme", "Again", null, null, null, false, create: true));
        _ = await Assert.ThrowsExactlyAsync<PartnerKeyException>(
            () => service.SaveClient("acme", "Acme", 0, null, null, false, create: false));
        _ = await Assert.ThrowsExactlyAsync<PartnerKeyException>(
            () => service.SaveClient("other", "Other", null, null, null, false, create: false));
    }

    [TestMethod]
    public async Task SaveClient_GivesTheManualCommandWithoutAVault()
    {
        var context = NewContext();
        var service = new PartnerKeyService(context, new FakeVault(connected: false), Configuration());

        var manual = await service.SaveClient("acme", "Acme", null, null, 10, false, create: true);

        StringAssert.Contains(manual, "--name client-acme");
        StringAssert.Contains(manual, "{\"name\":\"Acme\",\"dailyCap\":10,\"status\":\"active\"}");
    }

    [TestMethod]
    public async Task Developers_AreLinkedPerClient()
    {
        var (service, _, context) = await WithClient();
        _ = await service.SaveClient("other", "Other", null, null, null, false, create: true);
        var user = new ApplicationUser { Id = "u1", UserName = "dev1" };
        context.Users.Add(user);
        _ = await context.SaveChangesAsync();

        await service.AddDeveloper("acme", user);
        await service.AddDeveloper("acme", user);
        await service.AddDeveloper("other", user);

        Assert.IsTrue(await service.IsDeveloper("acme", "u1"));
        Assert.HasCount(2, await service.ClientsForUser("u1"));
        Assert.IsTrue(await service.RemoveDeveloper("acme", "u1"), "still a developer for other");
        Assert.IsFalse(await service.IsDeveloper("acme", "u1"));
        Assert.IsFalse(await service.RemoveDeveloper("other", "u1"));
        _ = await Assert.ThrowsExactlyAsync<PartnerKeyException>(() => service.AddDeveloper("nobody", user));
    }
}
