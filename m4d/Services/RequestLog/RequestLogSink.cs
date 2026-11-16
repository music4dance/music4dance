using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace m4d.Services.RequestLog;

/// <summary>Where flushed request-log blocks go.</summary>
public interface IRequestLogSink
{
    /// <summary>
    /// Appends the first <paramref name="count"/> bytes of <paramref name="buffer"/> to the blob.
    /// The caller reuses the buffer once this returns.
    /// </summary>
    Task AppendAsync(string blobName, byte[] buffer, int count, CancellationToken cancellationToken);
}

/// <summary>
/// Appends to hourly append blobs in one container. Each block is appended atomically, so
/// several instances can share an hour's blob. The container must already exist; the app's
/// identity needs Storage Blob Data Contributor on it.
/// </summary>
public sealed class BlobRequestLogSink(BlobContainerClient container) : IRequestLogSink
{
    // The flush service is the only caller, one flush at a time.
    private string _createdBlob;

    public async Task AppendAsync(string blobName, byte[] buffer, int count,
        CancellationToken cancellationToken)
    {
        var blob = container.GetAppendBlobClient(blobName);
        if (_createdBlob != blobName)
        {
            await blob.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _createdBlob = blobName;
        }

        using var content = new MemoryStream(buffer, 0, count, writable: false);
        await blob.AppendBlockAsync(content, cancellationToken: cancellationToken);
    }
}
