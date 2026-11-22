namespace m4dModels;

/// <summary>
/// Names one artist is credited under: the first of each list is the one to use, and the rest are
/// the same artist. Kept in code while it's short; names compare by their letters and digits
/// alone (Key), so case, accents, spaces and punctuation don't matter. Used where the shared-id
/// review compares credits and names a merged song (architecture/songs/shared-id-review.md).
/// </summary>
public static class ArtistAliases
{
    private static readonly string[][] Aliases =
    [
        [
            "101 Strings Orchestra", "101 String Orchestra", "101 Strings", "101 Strings Orch",
            "The 101 Strings Orchestra", "New 101 Strings Orchestra", "The New 101 Strings Orchestra",
            "New 101 String Orchestra", "The New 101 String Orchestra", "The New 101 Strings Orch",
        ],
        ["Lady A", "Lady Antebellum"],
        ["P!nk", "Pink"],
        ["Orquesta Yambao", "Yambao Orquesta"],
        ["El Gran Combo De Puerto Rico", "El Gran Combo"],
        ["Michael Paul Kramer", "Michael P. Kramer"],
        ["Sasha Sloan", "Sasha Alex Sloan"],
        ["Kesha", "Ke$ha"],
        ["Village People", "The Village People"],
        ["Bobby V.", "Bobby Valentino"],
        ["Café Latino", "Cafe Latino"],
        ["Son By Four", "Son By 4"],
    ];

    // Spellings that only differ by accent or punctuation share a key ("Café Latino" is to be
    // used for "Cafe Latino"); the first list to name a key wins
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
