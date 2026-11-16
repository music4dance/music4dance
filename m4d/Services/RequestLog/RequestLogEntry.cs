using System.Buffers;
using System.Text.Json;

namespace m4d.Services.RequestLog;

/// <summary>
/// One request, as written to the app request log. The field names in the JSON match the App
/// Service HTTP log's (CIp, CsUriStem, ScStatus, ...) so the analyze-traffic skill reads both;
/// RelayIp is extra. Cookie holds cookie names only ("_ga=; music4dance="), never values.
/// </summary>
public sealed record RequestLogEntry(
    DateTime Time,
    string ClientIp,
    string RelayIp,
    string Host,
    string Method,
    string Path,
    string Query,
    int Status,
    long TimeTakenMs,
    long ResponseBytes,
    long RequestBytes,
    string UserAgent,
    string Referer,
    string CookieNames,
    string Protocol)
{
    /// <summary>
    /// Bytes per append. Append blobs take up to 4 MiB a block; 1 MiB keeps the reused flush
    /// buffer small and still covers a typical minute (about 250 requests, 100 KB) in one append.
    /// </summary>
    public const int MaxBlockBytes = 1024 * 1024;

    /// <summary>
    /// The hourly blob for <paramref name="time"/>, in the same y=/m=/d=/h= layout as the App
    /// Service HTTP logs: {prefix}/y=2026/m=11/d=15/h=07/m=00/PT1H.json (UTC).
    /// </summary>
    public static string BlobName(string prefix, DateTime time)
    {
        var t = time.ToUniversalTime();
        return $"{prefix}/y={t:yyyy}/m={t:MM}/d={t:dd}/h={t:HH}/m=00/PT1H.json";
    }

    public void WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("time", Time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        writer.WriteString("CIp", ClientIp ?? "");
        writer.WriteString("RelayIp", RelayIp ?? "");
        writer.WriteString("CsHost", Host ?? "");
        writer.WriteString("CsMethod", Method ?? "");
        writer.WriteString("CsUriStem", Path ?? "");
        writer.WriteString("CsUriQuery", Query ?? "");
        writer.WriteNumber("ScStatus", Status);
        writer.WriteNumber("TimeTaken", TimeTakenMs);
        writer.WriteNumber("ScBytes", ResponseBytes);
        writer.WriteNumber("CsBytes", RequestBytes);
        writer.WriteString("UserAgent", UserAgent ?? "");
        writer.WriteString("Referer", Referer ?? "");
        writer.WriteString("Cookie", CookieNames ?? "");
        writer.WriteString("Protocol", Protocol ?? "");
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the entries as JSON lines into <paramref name="buffer"/>, calling
    /// <paramref name="appendAsync"/> with each full block (cut at a line boundary) and with the
    /// remainder. The caller owns and reuses the buffer, so a flush allocates one small line
    /// buffer rather than a copy of the whole batch. A line longer than the buffer is skipped.
    /// Returns the number of entries written.
    /// </summary>
    public static async Task<int> WriteBlocksAsync(IEnumerable<RequestLogEntry> entries,
        byte[] buffer, Func<byte[], int, Task> appendAsync)
    {
        var line = new ArrayBufferWriter<byte>(1024);
        using var writer = new Utf8JsonWriter(line);
        var used = 0;
        var written = 0;

        foreach (var entry in entries)
        {
            line.ResetWrittenCount();
            writer.Reset(line);
            entry.WriteJson(writer);
            writer.Flush();
            line.Write("\n"u8);

            var length = line.WrittenCount;
            if (length > buffer.Length)
            {
                continue;
            }
            if (used + length > buffer.Length)
            {
                await appendAsync(buffer, used);
                used = 0;
            }
            line.WrittenMemory.CopyTo(buffer.AsMemory(used));
            used += length;
            written++;
        }

        if (used > 0)
        {
            await appendAsync(buffer, used);
        }
        return written;
    }
}
