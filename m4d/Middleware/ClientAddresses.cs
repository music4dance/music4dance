using System.Net;

namespace m4d.Middleware;

/// <summary>
/// The request's addresses as CloudflareClientIpMiddleware resolved them, stored as a request
/// feature. Client is the visitor (CF-Connecting-IP when relayed by Cloudflare, otherwise the
/// connecting address); Relay is the address that handed the request to App Service (the
/// Cloudflare edge when proxied). Both are normalized (no IPv4-mapped IPv6 form), and either can
/// be null when the connection has no remote address.
/// </summary>
public sealed record ClientAddresses(IPAddress Client, IPAddress Relay);
