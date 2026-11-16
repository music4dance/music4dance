using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using m4d.Configuration;

namespace m4d.Services;

/// <summary>Where <see cref="RequestLogUploader"/> copies the request log: one append blob per file.</summary>
public interface IRequestLogStore
{
    /// <summary>The bytes already stored for <paramref name="name"/>; 0 when there's no blob yet.</summary>
    Task<long> GetLengthAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Appends <paramref name="content"/> to <paramref name="name"/>, creating the blob when
    /// <paramref name="offset"/> is 0. Fails if the blob's length isn't <paramref name="offset"/>,
    /// so a retried or overlapping append can never duplicate lines.
    /// </summary>
    Task AppendAsync(string name, long offset, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}

public sealed class BlobRequestLogStore(BlobContainerClient container) : IRequestLogStore
{
    public async Task<long> GetLengthAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var properties = await container.GetAppendBlobClient(name)
                .GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (RequestFailedException e) when (e.Status == StatusCodes.Status404NotFound)
        {
            return 0;
        }
    }

    public async Task AppendAsync(string name, long offset, ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var blob = container.GetAppendBlobClient(name);
        if (offset == 0)
        {
            await blob.CreateIfNotExistsAsync(new AppendBlobCreateOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain; charset=utf-8" },
            }, cancellationToken);
        }
        await using var stream = BinaryData.FromBytes(content).ToStream();
        await blob.AppendBlockAsync(stream, new AppendBlobAppendBlockOptions
        {
            Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = offset },
        }, cancellationToken);
    }
}

/// <summary>
/// Copies the app request log from RequestLog:Directory (on /home) to blob storage, where a
/// lifecycle rule keeps it 30 days. Starts with the app; first pass after
/// <see cref="FirstDelay"/>, then every <see cref="Interval"/>. Each pass appends to each file's
/// blob whatever whole lines the file has gained since, so it keeps no state: after a restart it
/// picks up from the blobs' lengths. A failure is a warning and is retried next pass; the files
/// stay on /home for RequestLog:RetainedFileCountLimit days meanwhile.
/// See architecture/observability/logging-and-diagnostics.md (App request log).
/// </summary>
public class RequestLogUploader(
    RequestLogSetup.Settings settings, IRequestLogStore store, ILogger<RequestLogUploader> logger)
    : BackgroundService
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>The most appended at once; replaceable for tests.</summary>
    public int ChunkBytes { get; init; } = 1024 * 1024;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstDelay, stoppingToken);
            while (true)
            {
                await UploadAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; the next process catches up from the files on /home.
        }
    }

    /// <summary>One pass over every request log file. Returns the bytes uploaded.</summary>
    public async Task<long> UploadAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(settings.Directory))
        {
            return 0;
        }

        long total = 0;
        var files = 0;
        foreach (var path in Directory.GetFiles(settings.Directory, RequestLogSetup.FileNamePrefix + "*.txt").Order())
        {
            try
            {
                var uploaded = await UploadFileAsync(path, cancellationToken);
                total += uploaded;
                files += uploaded > 0 ? 1 : 0;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "[RequestLog] Couldn't upload {File}; retrying next pass",
                    Path.GetFileName(path));
            }
        }
        if (total > 0)
        {
            logger.LogInformation("[RequestLog] Uploaded {Bytes} bytes from {Files} file(s)", total, files);
        }
        return total;
    }

    private async Task<long> UploadFileAsync(string path, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(path);
        var offset = await store.GetLengthAsync(name, cancellationToken);
        // The logger is appending to the newest file and may delete the oldest.
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (offset > file.Length)
        {
            logger.LogWarning("[RequestLog] {File} is shorter than its blob ({Length} < {Offset}); skipping it",
                name, file.Length, offset);
            return 0;
        }

        var start = offset;
        byte[] buffer = null;
        while (offset < file.Length)
        {
            buffer ??= new byte[ChunkBytes];
            file.Seek(offset, SeekOrigin.Begin);
            var read = await file.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
            // Whole lines only: the logger may be partway through writing the last one. A chunk
            // with no line end at all (a line longer than ChunkBytes) goes as is, so it can't stall.
            var lineEnd = buffer.AsSpan(0, read).LastIndexOf((byte)'\n');
            var length = lineEnd >= 0 ? lineEnd + 1 : read == buffer.Length ? read : 0;
            if (length == 0)
            {
                break;
            }
            await store.AppendAsync(name, offset, buffer.AsMemory(0, length), cancellationToken);
            offset += length;
            if (read < buffer.Length)
            {
                break;
            }
        }
        return offset - start;
    }
}
