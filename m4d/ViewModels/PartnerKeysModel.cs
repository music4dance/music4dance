using m4d.PublicApi;

namespace m4d.ViewModels;

public class PartnerKeysModel
{
    public IReadOnlyList<PartnerClient> Clients { get; init; } = [];
    public bool IsLive { get; init; }
    public string Message { get; init; }
    public string Error { get; init; }
}

public class IssuedKeyModel
{
    public IssuedKey Key { get; init; }
    public string ReturnAction { get; init; }
}

public class PartnerUsageModel
{
    public PartnerClient Client { get; init; }
    public PartnerUsageReport Report { get; init; }

    /// <summary>False when PartnerApi:UsageTableUri isn't set, so there's nothing to read.</summary>
    public bool Connected { get; init; }

    public bool IsAdmin { get; init; }
    public string Error { get; init; }
}

public class PartnerClientsModel
{
    public IReadOnlyList<PartnerClient> Clients { get; init; } = [];
    public bool VaultConnected { get; init; }
    public bool IsLive { get; init; }
    public string Message { get; init; }
    public string Error { get; init; }
    public string ManualCommand { get; init; }
}
