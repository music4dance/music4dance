using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using m4d.Configuration;

namespace m4d.Services;

/// <summary>Where <see cref="LogFileUploader"/> copies a log: one append blob per file.</summary>
public interface ILogFileStore
{
    /// <summary>The length of every stored blob whose name starts with <paramref name="prefix"/>.</summary>
    Task<IReadOnlyDictionary<string, long>> GetLengthsAsync(string prefix, CancellationToken cancellationToken);

    /// <summary>
    /// Appends <paramref name="content"/> to <paramref name="name"/>, creating the blob when
    /// <paramref name="offset"/> is 0. Fails if the blob's length isn't <paramref name="offset"/>,
    /// so a retried or overlapping append can never duplicate lines.
    /// </summary>
    Task AppendAsync(string name, long offset, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}

public sealed class BlobLogFileStore(BlobContainerClient container) : ILogFileStore
{
    public async Task<IReadOnlyDictionary<string, long>> GetLengthsAsync(string prefix,
        CancellationToken cancellationToken)
    {
        var lengths = new Dictionary<string, long>();
        await foreach (var blob in container.GetBlobsAsync(
            new GetBlobsOptions { Prefix = prefix }, cancellationToken))
        {
            lengths[blob.Name] = blob.Properties.ContentLength ?? 0;
        }
        return lengths;
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
/// One log for <see cref="LogFileUploader"/> to copy: the files in <paramref name="Directory"/>
/// named <paramref name="FilePrefix"/>*.txt.
/// </summary>
/// <param name="Name">The tag on the uploader's own log messages (<c>RequestLog</c>).</param>
/// <param name="DeleteUploaded">Whether the uploader deletes a file once it's copied and
/// <see cref="LogFileUploader.KeepAfterUpload"/> old. False when the logger that writes the files
/// manages its own retention (the App Service file logger does).</param>
public sealed record LogUploadSource(string Name, string Directory, string FilePrefix, bool DeleteUploaded);

/// <summary>
/// Copies a log's files from /home to blob storage, where a lifecycle rule keeps them 30 days:
/// the app request log (<see cref="RequestLogSetup"/>) and the App Service file logger's
/// Warning-and-above app log (<see cref="AppLogUploadSetup"/>), one uploader each. Starts with the
/// app; first pass after <see cref="FirstDelay"/>, then every <see cref="Interval"/>.
/// <list type="bullet">
/// <item>Each pass lists the blobs once, then appends to each file's blob whatever whole lines
/// the file has gained, so it keeps no state: after a restart it picks up from the blobs'
/// lengths.</item>
/// <item>When <see cref="LogUploadSource.DeleteUploaded"/>, it deletes a file once it's fully
/// copied and hasn't been written for <see cref="KeepAfterUpload"/>. While uploads fail, files
/// stay until the logger's RetainedFileCountLimit.</item>
/// <item>It handles every matching file in the directory, not just this instance's, so files
/// a previous instance left behind still get copied and cleaned up. Two uploaders on one file
/// are safe: the source bytes are the same and each append is conditioned on the blob's
/// length.</item>
/// <item>Nothing a pass throws escapes: a failure is a warning, retried next pass. An exception
/// out of a BackgroundService stops the host by default, which would take the site down.</item>
/// </list>
/// See architecture/observability/logging-and-diagnostics.md (App request log, App log upload).
/// </summary>
public class LogFileUploader(LogUploadSource source, ILogFileStore store, ILogger<LogFileUploader> logger)
    : BackgroundService
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public static readonly TimeSpan KeepAfterUpload = TimeSpan.FromDays(2);

    /// <summary>The most appended at once; replaceable for tests.</summary>
    public int ChunkBytes { get; init; } = 1024 * 1024;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstDelay, stoppingToken);
            while (true)
            {
                await TryUploadAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; the next process catches up from the files on /home.
        }
    }

    /// <summary><see cref="UploadAsync"/>, with any failure other than shutdown logged.</summary>
    public async Task TryUploadAsync(CancellationToken stoppingToken)
    {
        try
        {
            await UploadAsync(stoppingToken);
        }
        catch (Exception e) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "[{Log}] Upload pass failed; retrying next pass", source.Name);
        }
    }

    /// <summary>One pass over every file of the log. Returns the bytes uploaded.</summary>
    public async Task<long> UploadAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(source.Directory))
        {
            return 0;
        }

        var files = new DirectoryInfo(source.Directory)
            .GetFiles(source.FilePrefix + "*.txt")
            .OrderBy(f => f.Name)
            .ToList();
        if (files.Count == 0)
        {
            return 0;
        }

        var stored = await store.GetLengthsAsync(source.FilePrefix, cancellationToken);
        long total = 0;
        var uploadedFiles = 0;
        foreach (var file in files)
        {
            try
            {
                var offset = stored.GetValueOrDefault(file.Name);
                long length;
                // The logger is appending to the newest file and may delete the oldest. Take the
                // length from the open stream: a directory listing's can be stale for a file
                // that's open for writing (Windows).
                await using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    length = stream.Length;
                    if (length > offset)
                    {
                        var uploaded = await UploadFileAsync(stream, file.Name, offset, cancellationToken);
                        total += uploaded;
                        uploadedFiles += uploaded > 0 ? 1 : 0;
                    }
                }
                if (length < offset)
                {
                    logger.LogWarning("[{Log}] {File} is shorter than its blob ({Length} < {Offset}); skipping it",
                        source.Name, file.Name, length, offset);
                }
                else if (source.DeleteUploaded && length == offset
                    && file.LastWriteTimeUtc < DateTime.UtcNow - KeepAfterUpload)
                {
                    file.Delete();
                }
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "[{Log}] Couldn't upload {File}; retrying next pass", source.Name, file.Name);
            }
        }
        if (total > 0)
        {
            logger.LogInformation("[{Log}] Uploaded {Bytes} bytes from {Files} file(s)", source.Name, total, uploadedFiles);
        }
        return total;
    }

    private async Task<long> UploadFileAsync(FileStream file, string name, long offset,
        CancellationToken cancellationToken)
    {
        var start = offset;
        var buffer = new byte[ChunkBytes];
        while (offset < file.Length)
        {
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
