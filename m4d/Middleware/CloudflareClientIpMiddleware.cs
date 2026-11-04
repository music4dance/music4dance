using System.Net;
using System.Net.Sockets;

using m4d.Security;

namespace m4d.Middleware;

/// <summary>
/// Behind Cloudflare's proxy, the connection the app sees comes from a Cloudflare edge server,
/// not the visitor. Cloudflare passes the visitor's address in CF-Connecting-IP. This
/// middleware sets Connection.RemoteIpAddress to that address, so the identity rate limits,
/// lockouts, login logging and /Admin/Diagnostics keep seeing visitors rather than lumping
/// everyone behind one edge server together.
///
/// The header is trusted only when the request provably came from Cloudflare: the address
/// that handed the request to App Service has to be inside CloudflareClientIp:IpRanges.
/// That address is RemoteIpAddress when App Service's forwarded-header handling has already
/// applied its X-Forwarded-For. When it hasn't, RemoteIpAddress is App Service's own
/// private front-end address, and the front end's appended (last) X-Forwarded-For entry is
/// used instead. A request that skips Cloudflare (msc4dnc.azurewebsites.net, local dev) is
/// left alone, so a client can't pick its own IP by sending the header itself.
///
/// Must run before anything that reads RemoteIpAddress (authentication, rate limiting). See
/// architecture/plans/cloudflare-migration.md (Phase 1).
/// </summary>
public class CloudflareClientIpMiddleware
{
    public const string ConnectingIpHeader = "CF-Connecting-IP";
    public const string ForwardedForHeader = "X-Forwarded-For";

    private readonly RequestDelegate _next;
    private readonly bool _enabled;
    private readonly IReadOnlyList<IPNetwork> _networks;

    public CloudflareClientIpMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<CloudflareClientIpMiddleware> logger)
    {
        _next = next;
        var options = CloudflareClientIpOptions.FromConfiguration(configuration);
        _enabled = options.Enabled;
        _networks = options.Networks;

        if (options.InvalidRanges.Count > 0)
        {
            logger.LogWarning("CloudflareClientIp: ignoring invalid ranges {Ranges}",
                string.Join(";", options.InvalidRanges));
        }
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (_enabled)
        {
            var clientIp = ResolveClientIp(
                context.Connection.RemoteIpAddress,
                context.Request.Headers[ForwardedForHeader].ToString(),
                context.Request.Headers[ConnectingIpHeader].ToString(),
                _networks);
            if (clientIp != null)
            {
                context.Connection.RemoteIpAddress = clientIp;
            }
        }

        return _next(context);
    }

    /// <summary>
    /// The visitor's address from <paramref name="connectingIp"/> when the request was relayed
    /// by Cloudflare, otherwise null (keep RemoteIpAddress as it is).
    /// </summary>
    public static IPAddress ResolveClientIp(IPAddress remoteIp, string forwardedFor,
        string connectingIp, IReadOnlyList<IPNetwork> cloudflareNetworks)
    {
        if (remoteIp == null || string.IsNullOrWhiteSpace(connectingIp)
            || !IPAddress.TryParse(connectingIp.Trim(), out var visitor))
        {
            return null;
        }

        var relay = Normalize(remoteIp);
        if (IsPrivate(relay))
        {
            relay = LastForwardedFor(forwardedFor);
        }

        return relay != null && cloudflareNetworks.Any(n => n.Contains(relay))
            ? Normalize(visitor)
            : null;
    }

    private static IPAddress LastForwardedFor(string forwardedFor)
    {
        var last = (forwardedFor ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries).LastOrDefault();
        if (last == null)
        {
            return null;
        }

        // App Service appends "ip:port"; IPEndPoint also accepts a bare address and "[v6]:port".
        if (IPAddress.TryParse(last, out var address))
        {
            return Normalize(address);
        }
        return IPEndPoint.TryParse(last, out var endpoint) ? Normalize(endpoint.Address) : null;
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static readonly IPNetwork[] PrivateNetworks =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("fc00::/7"),
    ];

    /// <summary>
    /// Addresses only App Service's own front end (or a local client) connects from.
    /// </summary>
    private static bool IsPrivate(IPAddress address) =>
        IPAddress.IsLoopback(address)
        || (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv6LinkLocal)
        || PrivateNetworks.Any(n => n.Contains(address));
}
