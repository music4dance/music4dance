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

public class PartnerClientsModel
{
    public IReadOnlyList<PartnerClient> Clients { get; init; } = [];
    public bool VaultConnected { get; init; }
    public bool IsLive { get; init; }
    public string Message { get; init; }
    public string Error { get; init; }
    public string ManualCommand { get; init; }
}
