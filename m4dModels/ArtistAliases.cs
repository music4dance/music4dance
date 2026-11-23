using Newtonsoft.Json.Linq;

namespace m4dModels;

/// <summary>
/// Names one artist is credited under: the first of each list is the one to use, and the rest are
/// the same artist. The table is m4d/ClientApp/src/models/artist-aliases.json, embedded here and
/// imported by the client (ArtistNames.ts), so both sides fold the same names. Names compare by
/// their letters and digits alone (Key), so case, accents, spaces and punctuation don't matter.
/// Used where individual artists are produced and read, and where the shared-id review compares
/// credits and names a merged song (architecture/songs/individual-artists.md).
/// </summary>
public static class ArtistAliases
{
    private const string ResourceName = "m4dModels.artist-aliases.json";

    private static readonly string[][] Aliases = Load();

    private static string[][] Load()
    {
        using var stream = typeof(ArtistAliases).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}");
        using var reader = new StreamReader(stream);
        var aliases = JObject.Parse(reader.ReadToEnd())["aliases"];
        return aliases == null
            ? []
            : [.. aliases.Select(names => names.Select(n => (string)n).ToArray()).Where(names => names.Length > 0)];
    }

    private static readonly Dictionary<string, string> Preferred = Aliases
        .SelectMany(names => names.Select(n => (Key: Key(n), Preferred: names[0])))
        .DistinctBy(a => a.Key)
        .ToDictionary(a => a.Key, a => a.Preferred);

    // The name's letters and digits, lowercased and without accents
    private static string Key(string name) =>
        new([.. ArtistSplitter.ArtistKey(name ?? "").Where(char.IsLetterOrDigit)]);

    /// <summary>The name to use for an artist: "Pink" is P!nk; a name without aliases comes back as is.</summary>
    public static string PreferredName(string name) =>
        string.IsNullOrWhiteSpace(name) ? name : Preferred.GetValueOrDefault(Key(name), name);

    /// <summary>
    /// The credit with its primary artist under the name to use: "Pink feat. Nate Ruess" is "P!nk
    /// feat. Nate Ruess", and "Lady Antebellum" is "Lady A".
    /// </summary>
    public static string PreferredCredit(string credit)
    {
        if (string.IsNullOrWhiteSpace(credit))
        {
            return credit;
        }

        var whole = PreferredName(credit.Trim());
        if (whole != credit.Trim())
        {
            return whole;
        }

        var primary = ArtistSplitter.RemoveArtistFeaturing(credit).Trim();
        var preferred = PreferredName(primary);
        return preferred != primary && credit.TrimStart().StartsWith(primary, StringComparison.OrdinalIgnoreCase)
            ? preferred + credit.TrimStart()[primary.Length..]
            : credit;
    }
}
