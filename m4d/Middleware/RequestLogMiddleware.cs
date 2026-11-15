using System.Diagnostics;
using System.Net;

using m4d.Security;
using m4d.Services.RequestLog;

using Microsoft.AspNetCore.Http.Features;

namespace m4d.Middleware;

/// <summary>
/// Records one entry per request in the app request log (RequestLogQueue, flushed to blob
/// storage by RequestLogFlushService). Behind Cloudflare, App Service's own HTTP log records
/// Cloudflare's edge address as the client; this log records the visitor's, resolved the same
/// way CloudflareClientIpMiddleware does, plus the edge address as RelayIp.
///
/// Registered first in the pipeline, so it sees static files (which are served before
/// CloudflareClientIpMiddleware runs), the final status after the exception handler, and the
/// bytes actually sent (after compression).
/// </summary>
public class RequestLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestLogQueue _queue;
    private readonly bool _resolveCloudflare;
    private readonly IReadOnlyList<IPNetwork> _cloudflareNetworks;

    public RequestLogMiddleware(RequestDelegate next, RequestLogQueue queue, IConfiguration configuration)
    {
        _next = next;
        _queue = queue;
        var cloudflare = CloudflareClientIpOptions.FromConfiguration(configuration);
        _resolveCloudflare = cloudflare.Enabled;
        _cloudflareNetworks = cloudflare.Networks;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var time = DateTime.UtcNow;
        var started = Stopwatch.GetTimestamp();
        var remoteIp = context.Connection.RemoteIpAddress;

        var originalBody = context.Features.Get<IHttpResponseBodyFeature>();
        var counter = originalBody == null ? null : new CountingStream(originalBody.Stream);
        var countingBody = counter == null ? null : new StreamResponseBodyFeature(counter, originalBody);
        if (countingBody != null)
        {
            context.Features.Set<IHttpResponseBodyFeature>(countingBody);
        }

        var failed = false;
        try
        {
            await _next(context);
            if (countingBody != null)
            {
                // Flushes anything written through BodyWriter into the counter.
                await countingBody.CompleteAsync();
            }
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            if (countingBody != null)
            {
                context.Features.Set(originalBody);
            }

            try
            {
                _queue.Add(CreateEntry(context, time, started, remoteIp, counter?.BytesWritten ?? 0, failed));
            }
            catch
            {
                // Logging a request must never fail it.
            }
        }
    }

    private RequestLogEntry CreateEntry(HttpContext context, DateTime time, long started,
        IPAddress remoteIp, long responseBytes, bool failed)
    {
        var request = context.Request;
        var forwardedFor = request.Headers[CloudflareClientIpMiddleware.ForwardedForHeader].ToString();
        var relay = CloudflareClientIpMiddleware.RelayAddress(remoteIp, forwardedFor);
        var client = (_resolveCloudflare
                ? CloudflareClientIpMiddleware.ResolveClientIp(remoteIp, forwardedFor,
                    request.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader].ToString(),
                    _cloudflareNetworks)
                : null)
            ?? relay ?? remoteIp;

        var status = failed && !context.Response.HasStarted
            ? StatusCodes.Status500InternalServerError
            : context.Response.StatusCode;

        return new RequestLogEntry(
            Time: time,
            ClientIp: client?.ToString() ?? "",
            RelayIp: (relay ?? remoteIp)?.ToString() ?? "",
            Host: request.Host.Value ?? "",
            Method: request.Method,
            Path: request.Path.Value ?? "",
            Query: request.QueryString.HasValue ? request.QueryString.Value[1..] : "",
            Status: status,
            TimeTakenMs: (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            ResponseBytes: responseBytes,
            RequestBytes: request.ContentLength ?? 0,
            UserAgent: request.Headers.UserAgent.ToString(),
            Referer: request.Headers.Referer.ToString(),
            CookieNames: CookieNames(request),
            Protocol: request.Protocol);
    }

    /// <summary>"name1=; name2=": enough to tell which cookies were sent, with no values.</summary>
    internal static string CookieNames(HttpRequest request) =>
        request.Headers.Cookie.Count == 0
            ? ""
            : string.Join("; ", request.Cookies.Keys.Select(k => k + "="));

    /// <summary>Passes writes through to the response stream, counting the bytes.</summary>
    internal sealed class CountingStream(Stream inner) : Stream
    {
        private long _bytesWritten;

        public long BytesWritten => Interlocked.Read(ref _bytesWritten);

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            Interlocked.Add(ref _bytesWritten, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            inner.Write(buffer);
            Interlocked.Add(ref _bytesWritten, buffer.Length);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
            Interlocked.Add(ref _bytesWritten, buffer.Length);
        }
    }
}
