using CsvHelper;

namespace m4d.Security;

/// <summary>
/// Writes the 4xx URL export served by /Admin/Http4xxExportCsv (RFC 4180 CSV).
/// </summary>
public static class Http4xxCsv
{
    public static void Write(TextWriter writer, IEnumerable<Http4xxUrlStats> urls)
    {
        using var csv = CsvWriter.Create(writer, CsvWriterOptions.Default, leaveOpen: true);
        csv.WriteHeader(["Url", "Status", "Count", "LastSeen"]);

        foreach (var url in urls)
        {
            csv.WriteField(url.Url);
            csv.WriteField(url.StatusCode);
            csv.WriteField(url.Count);
            csv.WriteField(url.LastSeen);
            csv.EndRecord();
        }
    }
}
