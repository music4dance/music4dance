namespace m4dModels;

/// <summary>
/// A partner that calls the standalone partner API (m4d-api) with its own API keys. The id is
/// also the client's name in the partner-keys vault (secret client-{Id}) and in the API's usage
/// table, so it can't change. The limits are copied to the vault, where the API reads them; null
/// means the API's default. See architecture/plans/partner-api-standalone-host.md.
/// </summary>
public class PartnerClient
{
    public string Id { get; set; }
    public string Name { get; set; }

    public double? RatePerSecond { get; set; }
    public int? Burst { get; set; }
    public long? DailyCap { get; set; }

    public bool Disabled { get; set; }

    public DateTimeOffset Created { get; set; }

    public ICollection<PartnerDeveloper> Developers { get; set; } = [];
    public ICollection<PartnerApiKey> Keys { get; set; } = [];
}

/// <summary>A music4dance account that can issue and revoke keys for a partner client.</summary>
public class PartnerDeveloper
{
    public string ClientId { get; set; }
    public string UserId { get; set; }

    public PartnerClient Client { get; set; }
    public ApplicationUser User { get; set; }
}

/// <summary>
/// The metadata of an issued API key, for listing, auditing and revoking it. Neither the key nor
/// its hash is stored here: the hash lives only in the partner-keys vault (secret key-{KeyId}),
/// and the key only with the partner.
/// </summary>
public class PartnerApiKey
{
    public string KeyId { get; set; }
    public string ClientId { get; set; }
    public string Label { get; set; }

    public DateTimeOffset Created { get; set; }
    public string CreatedBy { get; set; }

    public DateTimeOffset? Revoked { get; set; }
    public string RevokedBy { get; set; }

    public PartnerClient Client { get; set; }

    public bool IsActive => Revoked == null;
}
