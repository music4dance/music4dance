using System.Net;

namespace m4d.Security;

/// <summary>
/// Settings for CloudflareClientIpMiddleware, from the CloudflareClientIp configuration
/// section. Read once at startup.
/// </summary>
public class CloudflareClientIpOptions
{
    public const string SectionName = "CloudflareClientIp";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// ';'-separated CIDR ranges that Cloudflare proxies from, as published at
    /// https://www.cloudflare.com/ips-v4 and https://www.cloudflare.com/ips-v6 (checked
    /// 2026-11-04). Only a request relayed from one of these has its CF-Connecting-IP trusted.
    /// </summary>
    public string IpRanges { get; set; } =
        "173.245.48.0/20;103.21.244.0/22;103.22.200.0/22;103.31.4.0/22;141.101.64.0/18;" +
        "108.162.192.0/18;190.93.240.0/20;188.114.96.0/20;197.234.240.0/22;198.41.128.0/17;" +
        "162.158.0.0/15;104.16.0.0/13;104.24.0.0/14;172.64.0.0/13;131.0.72.0/22;" +
        "2400:cb00::/32;2606:4700::/32;2803:f800::/32;2405:b500::/32;2405:8100::/32;" +
        "2a06:98c0::/29;2c0f:f248::/32";

    /// <summary>The ranges that parse; anything else is skipped (see InvalidRanges).</summary>
    public IReadOnlyList<IPNetwork> Networks =>
        [.. RangeList.Select(r => (Valid: IPNetwork.TryParse(r, out var network), Network: network))
            .Where(r => r.Valid).Select(r => r.Network)];

    public IReadOnlyList<string> InvalidRanges =>
        [.. RangeList.Where(r => !IPNetwork.TryParse(r, out _))];

    private IEnumerable<string> RangeList =>
        (IpRanges ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static CloudflareClientIpOptions FromConfiguration(IConfiguration configuration)
    {
        return configuration.GetSection(SectionName).Get<CloudflareClientIpOptions>()
            ?? new CloudflareClientIpOptions();
    }
}
