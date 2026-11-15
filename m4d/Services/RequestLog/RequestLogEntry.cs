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
    /// <summary>Append blobs take at most 4 MiB per block.</summary>
    public const int MaxBlockBytes = 4 * 1024 * 1024;

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
    /// The entries as JSON lines, cut at line boundaries into chunks of at most
    /// <paramref name="maxBlockBytes"/>.
    /// </summary>
    public static List<byte[]> ToBlocks(IEnumerable<RequestLogEntry> entries,
        int maxBlockBytes = MaxBlockBytes)
    {
        var blocks = new List<byte[]>();
        var block = new MemoryStream();
        var line = new MemoryStream();
        using var writer = new Utf8JsonWriter(line);

        foreach (var entry in entries)
        {
            line.SetLength(0);
            writer.Reset(line);
            entry.WriteJson(writer);
            writer.Flush();
            line.WriteByte((byte)'\n');

            if (block.Length > 0 && block.Length + line.Length > maxBlockBytes)
            {
                blocks.Add(block.ToArray());
                block.SetLength(0);
            }
            line.WriteTo(block);
        }

        if (block.Length > 0)
        {
            blocks.Add(block.ToArray());
        }
        return blocks;
    }
}
