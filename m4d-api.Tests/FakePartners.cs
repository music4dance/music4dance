using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace m4dApi.Tests;

/// <summary>An in-memory partner-keys vault.</summary>
public class FakeKeySource : IKeySource
{
    private readonly Dictionary<string, (string Value, SecretInfo Info)> _secrets = [];
    private int _version;

    public List<string> Reads { get; } = [];
    public bool Fail { get; set; }

    public FakeKeySource AddKey(string key, string clientId, bool enabled = true, DateTimeOffset? expires = null)
    {
        _ = PartnerKeyFormat.TryParse(key, out var keyId);
        return Set(PartnerKeyFormat.KeySecretName(keyId), PartnerKeyFormat.Hash(key), enabled, expires,
            new Dictionary<string, string> { [PartnerKeyFormat.ClientTag] = clientId });
    }

    public FakeKeySource AddClient(string clientId, ClientConfig config) =>
        Set(PartnerKeyFormat.ClientSecretName(clientId), config.ToJson());

    public FakeKeySource Set(string name, string value, bool enabled = true, DateTimeOffset? expires = null,
        Dictionary<string, string> tags = null)
    {
        _secrets[name] = (value, new SecretInfo(name, $"v{++_version}", enabled, expires, tags ?? []));
        return this;
    }

    public void Remove(string name) => _secrets.Remove(name);

    public Task<IReadOnlyList<SecretInfo>> List(CancellationToken cancellationToken) =>
        Fail
            ? throw new Azure.RequestFailedException(503, "vault down")
            : Task.FromResult<IReadOnlyList<SecretInfo>>([.. _secrets.Values.Select(s => s.Info)]);

    public Task<string> Value(string name, string version, CancellationToken cancellationToken)
    {
        Reads.Add(name);
        var secret = _secrets[name];
        return secret.Info.Enabled
            ? Task.FromResult(secret.Value)
            : throw new Azure.RequestFailedException(403, "Operation get is not allowed on a disabled secret");
    }
}

/// <summary>An in-memory usage table that adds like the real one.</summary>
public class FakeUsageTable : IUsageTable
{
    public Dictionary<(string Client, string Day), Dictionary<string, long>> Rows { get; } = [];
    public bool Fail { get; set; }

    public Task<long> Add(string clientId, string day, IReadOnlyDictionary<string, long> counts,
        CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new Azure.RequestFailedException(503, "storage down");
        }

        if (!Rows.TryGetValue((clientId, day), out var row))
        {
            Rows[(clientId, day)] = row = [];
        }

        foreach (var (column, count) in counts)
        {
            row[column] = row.GetValueOrDefault(column) + count;
            if (UsageColumns.IsCounted(column))
            {
                row[StorageUsageTable.TotalColumn] = row.GetValueOrDefault(StorageUsageTable.TotalColumn) + count;
            }
        }

        return Task.FromResult(row.GetValueOrDefault(StorageUsageTable.TotalColumn));
    }
}

/// <summary>Keeps every message logged at Warning or above.</summary>
public class ListLogger<T> : ILogger<T>
{
    public List<string> Warnings { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
        Func<TState, Exception, string> formatter)
    {
        if (logLevel >= LogLevel.Warning)
        {
            Warnings.Add(formatter(state, exception));
        }
    }
}

/// <summary>A gate wired to fakes, with one client and one key loaded.</summary>
public class FakePartners
{
    public const string ClientId = "test-partner";

    public FakeKeySource Vault { get; } = new();
    public FakeUsageTable Table { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero));
    public ListLogger<PartnerGate> GateLog { get; } = new();
    public PartnerKeyStore Store { get; }
    public UsageMeter Usage { get; }
    public PartnerGate Gate { get; }
    public string Key { get; }

    public FakePartners(ClientConfig client = null, PartnerDefaults defaults = null, bool load = true)
    {
        Key = PartnerKeyFormat.Generate(PartnerKeyFormat.TestPrefix, out _);
        Vault.AddKey(Key, ClientId);
        if (client != null)
        {
            Vault.AddClient(ClientId, client);
        }

        Store = new PartnerKeyStore(Vault, NullLogger<PartnerKeyStore>.Instance, Time);
        Usage = new UsageMeter(Table, NullLogger<UsageMeter>.Instance, Time);
        Gate = new PartnerGate(Store, new ClientRateLimiter(Time), Usage, defaults ?? new PartnerDefaults(),
            Time, GateLog);
        if (load)
        {
            Store.Refresh(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
