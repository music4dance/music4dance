using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;

using m4d.Services.RequestLog;

using Microsoft.AspNetCore.Http.Features;

namespace m4d.Middleware;

/// <summary>
/// Records one entry per request in the app request log (RequestLogQueue, flushed to blob
/// storage by RequestLogFlushService). Behind Cloudflare, App Service's own HTTP log records
/// Cloudflare's edge address as the client; this log records the visitor's, as resolved by
/// CloudflareClientIpMiddleware (the <see cref="ClientAddresses"/> feature), plus the edge
/// address as RelayIp.
///
/// Registered right after CloudflareClientIpMiddleware, ahead of everything else, so it sees
/// static files, the final status after the exception handler, and the bytes sent after
/// compression. Bytes are counted by a thin decorator over the existing body feature: it adds
/// no buffering and never starts or completes the response itself, so Kestrel still chooses
/// Content-Length or chunked exactly as it would without it.
/// </summary>
public class RequestLogMiddleware(RequestDelegate next, RequestLogQueue queue)
{
    /// <summary>The status nginx and App Service log for a client that went away mid-request.</summary>
    public const int ClientClosedRequest = 499;

    public async Task InvokeAsync(HttpContext context)
    {
        var time = DateTime.UtcNow;
        var started = Stopwatch.GetTimestamp();

        var originalBody = context.Features.GetRequiredFeature<IHttpResponseBodyFeature>();
        var countingBody = new CountingBodyFeature(originalBody);
        context.Features.Set<IHttpResponseBodyFeature>(countingBody);

        var failed = false;
        try
        {
            await next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            context.Features.Set(originalBody);

            try
            {
                queue.Add(CreateEntry(context, time, started, countingBody.BytesWritten, failed));
            }
            catch
            {
                // Logging a request must never fail it.
            }
        }
    }

    private static RequestLogEntry CreateEntry(HttpContext context, DateTime time, long started,
        long bytesWritten, bool failed)
    {
        var request = context.Request;
        var addresses = context.Features.Get<ClientAddresses>() ?? Fallback(context);

        // A client that gave up shows as 499, as in the platform log. An exception is a 500 even
        // after the response started: Kestrel aborts the connection, so the client never gets a
        // complete response with the status it was sent.
        var status = context.RequestAborted.IsCancellationRequested ? ClientClosedRequest
            : failed ? StatusCodes.Status500InternalServerError
            : context.Response.StatusCode;

        return new RequestLogEntry(
            Time: time,
            ClientIp: addresses.Client?.ToString() ?? "",
            RelayIp: addresses.Relay?.ToString() ?? "",
            Host: request.Host.Value ?? "",
            Method: request.Method,
            Path: request.Path.Value ?? "",
            Query: request.QueryString.HasValue ? request.QueryString.Value[1..] : "",
            Status: status,
            TimeTakenMs: (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            // Kestrel discards a HEAD response's body.
            ResponseBytes: HttpMethods.IsHead(request.Method) ? 0 : bytesWritten,
            RequestBytes: request.ContentLength ?? 0,
            UserAgent: request.Headers.UserAgent.ToString(),
            Referer: request.Headers.Referer.ToString(),
            CookieNames: CookieNames(request),
            Protocol: request.Protocol);
    }

    /// <summary>For a pipeline without CloudflareClientIpMiddleware: the connecting address.</summary>
    private static ClientAddresses Fallback(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is { IsIPv4MappedToIPv6: true })
        {
            remote = remote.MapToIPv4();
        }
        return new ClientAddresses(remote, remote);
    }

    /// <summary>"name1=; name2=": enough to tell which cookies were sent, with no values.</summary>
    internal static string CookieNames(HttpRequest request) =>
        request.Headers.Cookie.Count == 0
            ? ""
            : string.Join("; ", request.Cookies.Keys.Select(k => k + "="));

    /// <summary>
    /// Forwards everything to the prior body feature, counting the bytes written through its
    /// Stream, its Writer and SendFileAsync. The wrappers are created only when used.
    /// </summary>
    internal sealed class CountingBodyFeature(IHttpResponseBodyFeature inner) : IHttpResponseBodyFeature
    {
        private CountingStream _stream;
        private CountingPipeWriter _writer;
        private long _sentFileBytes;

        public long BytesWritten => (_stream?.BytesWritten ?? 0) + (_writer?.BytesWritten ?? 0) + _sentFileBytes;

        public Stream Stream => _stream ??= new CountingStream(inner.Stream);

        public PipeWriter Writer => _writer ??= new CountingPipeWriter(inner.Writer);

        public void DisableBuffering() => inner.DisableBuffering();

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            inner.StartAsync(cancellationToken);

        public Task CompleteAsync() => inner.CompleteAsync();

        public async Task SendFileAsync(string path, long offset, long? count,
            CancellationToken cancellationToken = default)
        {
            await inner.SendFileAsync(path, offset, count, cancellationToken);
            _sentFileBytes += count ?? Math.Max(0, new FileInfo(path).Length - offset);
        }
    }

    /// <summary>Passes writes through to the response stream, counting the bytes.</summary>
    internal sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesWritten { get; private set; }

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
            BytesWritten += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
            BytesWritten += buffer.Length;
        }
    }

    /// <summary>Forwards to the response's PipeWriter, counting advanced and written bytes.</summary>
    internal sealed class CountingPipeWriter(PipeWriter inner) : PipeWriter
    {
        public long BytesWritten { get; private set; }

        public override bool CanGetUnflushedBytes => inner.CanGetUnflushedBytes;
        public override long UnflushedBytes => inner.UnflushedBytes;

        public override void Advance(int bytes)
        {
            inner.Advance(bytes);
            BytesWritten += bytes;
        }

        public override Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);
        public override Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);
        public override void CancelPendingFlush() => inner.CancelPendingFlush();
        public override void Complete(Exception exception = null) => inner.Complete(exception);
        public override ValueTask CompleteAsync(Exception exception = null) => inner.CompleteAsync(exception);

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) =>
            inner.FlushAsync(cancellationToken);

        public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source,
            CancellationToken cancellationToken = default)
        {
            BytesWritten += source.Length;
            return inner.WriteAsync(source, cancellationToken);
        }
    }
}
