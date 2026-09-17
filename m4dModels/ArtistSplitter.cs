using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace m4dModels;

/// <summary>
/// Evidence about which names are real, standalone artists. Used by <see cref="ArtistSplitter"/>
/// to decide ambiguous splits ("Dolly Parton &amp; Kenny Rogers" splits because both halves are
/// known artists; "Simon &amp; Garfunkel" doesn't because neither half is).
/// </summary>
public interface IArtistKnowledge
{
    bool IsKnownArtist(string name);
}

/// <summary>
/// Who supplied a song's explicit <c>Artists</c> list. Precedence is User &gt; Service &gt; Heuristic.
/// </summary>
public enum ArtistsSource
{
    None,
    Heuristic,
    Service,
    User,
}

/// <summary>
/// Result of splitting an artist credit into individual artists.
/// </summary>
/// <param name="Artists">Normalized, ordered (primary first), de-duplicated individual artists.
/// Empty only when the credit is empty.</param>
/// <param name="Rules">Ids of the rules that fired (for reporting).</param>
/// <param name="Unresolved">Separators that were seen but deliberately not split (for reporting).</param>
public record ArtistSplit(
    IReadOnlyList<string> Artists,
    IReadOnlyList<string> Rules,
    IReadOnlyList<string> Unresolved)
{
    /// <summary>
    /// True when the split says something beyond "the credit is a single artist".
    /// </summary>
    public bool IsSplit(string artist)
    {
        var credit = ArtistSplitter.CleanName(artist);
        return !(Artists.Count == 0 && credit.Length == 0 ||
            Artists.Count == 1 && string.Equals(Artists[0], credit, StringComparison.Ordinal));
    }
}

/// <summary>
/// Heuristically splits a song's single <c>Artist</c> credit (plus any "feat." clause in its
/// title) into individual artists. Pure and dependency free so it can run in unit tests, over an
/// index backup file, and in the song save pipeline. See architecture/artist-index-plan.md §5.
/// </summary>
public static class ArtistSplitter
{
    /// <summary>
    /// Bump on any behavior change (including the protected lists below) so batch re-runs and
    /// analysis reports can be tied to a heuristic version.
    /// </summary>
    public const int Version = 2;

    public const char Delimiter = '|';

    // Whole act names that contain separators but must never be split. Most band names are
    // already protected by the evidence requirement (their halves aren't standalone artists);
    // this list is for the ones where evidence alone would get it wrong.
    private static readonly string[] ProtectedActs =
    [
        "Little Feat",
        "Earth, Wind & Fire",
        "Earth, Wind and Fire",
        "Crosby, Stills, Nash & Young",
        "Crosby, Stills & Nash",
        "Peter, Paul and Mary",
        "Peter, Paul & Mary",
        "Emerson, Lake & Palmer",
        "Tyler, the Creator",
        "Blood, Sweat & Tears",
        "Simon & Garfunkel",
        "Hall & Oates",
        "Brooks & Dunn",
        "Big & Rich",
        "Sam & Dave",
        "Ike & Tina Turner",
        "Wisin y Yandel",
        "Wisin & Yandel",
        "Dan + Shay",
        "Chloe x Halle",
        "Dimitri Vegas & Like Mike",
        "TOMORROW X TOGETHER",
        "Lil Nas X",
        "Monét X Change",
        "10,000 Maniacs",
        "Harry Connick, Jr.",
        "Sammy Davis, Jr.",
        "Hank Williams, Jr.",
    ];

    // Possessives that, starting the text after a join, mean the rest is the leader's backing
    // group: "Duke Ellington and His Orchestra", "Fruko Y Sus Tesos", "Igor und seine Oberkrainer".
    // The leader alone becomes the individual artist.
    private static readonly HashSet<string> Possessives = new(StringComparer.OrdinalIgnoreCase)
    {
        "his", "her", "their", "its", "sein", "seine", "seinem", "seinen", "seiner", "su", "sus",
    };

    // A join to nothing but these ("with Orchestra", "and Singers", "& The Orchestra") is also a
    // backing group
    private static readonly HashSet<string> GenericEnsembleWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "orchestra", "orchestre", "orquesta", "orquestra", "orchester", "tanzorchester", "band",
        "singers", "chorus", "choir", "ensemble", "quartet", "quintet", "trio", "combo", "conjunto",
        "strings", "rhythm",
    };

    // Words that make a multi-word name a named ensemble ("London Symphony Orchestra", "Berliner
    // Philharmoniker"), which is always its own individual artist
    private static readonly HashSet<string> EnsembleWords = new(
        GenericEnsembleWords.Concat(
        [
            "philharmonic", "philharmonia", "philharmoniker", "philharmonie", "symphony",
            "symphoniker", "symphonique", "sinfonietta", "sinfonica", "sinfonia", "chorale",
            "sextet", "septet", "octet", "soloists", "solisti", "consort", "collegium", "capella",
            "cappella", "orkest", "orkester", "orkestra", "filharmonie", "filarmonica",
        ]),
        StringComparer.OrdinalIgnoreCase);

    // Nouns that, starting the text after a join (optionally after an article), mean the whole
    // credit is one act: "Mumford & Sons", "Kool & The Gang", "Pistol Annies & Friends"
    private static readonly HashSet<string> ActNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "friends", "sons", "daughters", "company", "co", "all-stars", "girls", "boys", "brothers",
        "sisters", "gang", "crew", "family", "cast",
    };

    private static readonly HashSet<string> JoinWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "&", "+", "and", "y", "e", "et", "und",
    };

    // Articles that, starting the text after "&"/"and"/"y"/..., usually mean a band name ("Huey Lewis
    // & The News"): those joins only split when the leader is a known artist. Not applied to
    // "with"/"x"/"vs".
    private static readonly HashSet<string> Articles = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "la", "el", "los", "las", "le", "les", "die", "der", "das", "il", "lo",
    };

    private static readonly HashSet<string> ArticleSensitiveSeparators =
        new(StringComparer.OrdinalIgnoreCase) { "&", "+", "and", "y", "e", "et", "und" };

    private static readonly HashSet<string> Fillers = new(StringComparer.OrdinalIgnoreCase)
    {
        "etc", "etc.", "and others", "others",
        // Role annotations that show up as list entries in classical credits
        "alto", "soprano", "mezzo-soprano", "tenor", "baritone", "bass", "piano", "violin", "cello",
        "guitar", "organ", "drums", "vocals", "vocal", "conductor",
        // Bare ensemble words that aren't browsable artists on their own
        "orchestra", "chorus", "company", "ensemble", "cast",
    };

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex ArtistFeaturing = new(
        @"^(?<primary>.*?\S)\s*[\(\[]?\s*\b(?:feat\.?|ft\.|featuring)\s+(?<featured>[^\s&+,;/].*?)\s*[\)\]]?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ArtistWithSlash = new(
        @"^(?<primary>.*?\S)\s+w/\s*(?<featured>\S.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TitleBracketFeaturing = new(
        @"[\(\[]\s*(?:feat\.?|ft\.?|featuring)\s+(?<names>[^\)\]]+?)\s*[\)\]]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TitleBareFeaturing = new(
        @"(?:\s-\s*|\s)(?:feat\.|ft\.|featuring)\s+(?<names>[^\(\)\[\]]+?)(?:\s+-\s+[^\(\)\[\]]*|\s*[\(\[].*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SpacedSlash = new(@"\s+/\s+", RegexOptions.Compiled);

    private static readonly Regex AmbiguousSeparator = new(
        @"\s+(?<sep>&|\+|and|y|e|et|und|x|vs\.?|with)\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Suffix = new(
        @",\s*(?<suffix>Jr|Sr|Jnr|Snr|II|III)\b\.?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RolePrefix = new(
        @"^(?:vocals?\s+by|vocals?:|featuring|feat\.|ft\.|orquesta:|cantor:|conductor:|piano:)\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FinalConjunction = new(
        @"^(?<head>.*\S)\s+(?:&|and)\s+(?<tail>\S.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "His Band Ross Mitchell" - a backing-group label left in front of the leader's name
    private static readonly Regex LeadingGroupLabel = new(
        @"^(?:his|her|their)\s+(?:orchestra|band)\s+(?=\S)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LeadingConjunction = new(
        @"^(?:&|\+|and|with)\s+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const char MaskStart = '\uE000';

    public static ArtistSplit Split(string artist, string title = null, IArtistKnowledge knowledge = null)
    {
        var credit = CleanName(artist);
        if (credit.Length == 0)
        {
            return new ArtistSplit([], [], []);
        }

        var context = new SplitContext(knowledge);
        var names = new List<string>();

        if (IsProtectedAct(credit))
        {
            names.Add(credit);
        }
        else
        {
            var masked = context.Mask(credit);
            var featuring = ArtistFeaturing.Match(masked);
            if (!featuring.Success)
            {
                featuring = ArtistWithSlash.Match(masked);
            }

            if (featuring.Success)
            {
                context.AddRule("feat");
                names.AddRange(context.SplitSegment(featuring.Groups["primary"].Value, false));
                names.AddRange(context.SplitSegment(featuring.Groups["featured"].Value, true));
            }
            else
            {
                names.AddRange(context.SplitSegment(masked, false));
            }
        }

        foreach (var clause in TitleFeaturedClauses(title))
        {
            context.AddRule("title-feat");
            names.AddRange(context.SplitSegment(context.Mask(clause), true));
        }

        var artists = names
            .Select(context.Unmask)
            .Select(FinalizeName)
            .Where(n => n.Length > 0 && !IsFiller(n) && !IsBackingGroup(n))
            .DistinctBy(n => n.ToLowerInvariant())
            .ToList();

        if (artists.Count == 0)
        {
            artists.Add(credit);
        }

        return new ArtistSplit(artists, [.. context.Rules.Distinct()], [.. context.Unresolved.Distinct()]);
    }

    /// <summary>
    /// Last pass over one individual name: a role marker, conjunction or backing-group label the
    /// segmenting left in front of it is not part of the artist's name. "Featuring Hilary
    /// Alexander" is Hilary Alexander, "With Lester Young" is Lester Young, and "His Band Ross
    /// Mitchell" is Ross Mitchell. A name that is nothing but such a label ("His Orchestra",
    /// "Chorus") is dropped by the caller.
    /// </summary>
    private static string FinalizeName(string name)
    {
        var current = CleanName(name);
        for (var previous = string.Empty; current != previous;)
        {
            previous = current;
            current = RolePrefix.Replace(current, string.Empty);
            current = LeadingConjunction.Replace(current, string.Empty);
            current = LeadingGroupLabel.Replace(current, string.Empty);
            current = CleanName(current);
        }

        return current;
    }

    /// <summary>
    /// Trims, collapses whitespace, strips dangling brackets/punctuation, and replaces the list
    /// delimiter. Applied to credits and to every individual artist name.
    /// </summary>
    public static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var s = Whitespace.Replace(name.Replace(Delimiter, '/'), " ").Trim(' ', ',', ';', ':', '-');

        while (s.Length > 1)
        {
            if (s[0] == '(' && s[^1] == ')' || s[0] == '[' && s[^1] == ']')
            {
                s = s[1..^1].Trim();
            }
            else if (s[^1] is ')' or ']' && !s.Contains(s[^1] == ')' ? '(' : '['))
            {
                s = s[..^1].Trim();
            }
            else if (s[0] is '(' or '[' && !s.Contains(s[0] == '(' ? ')' : ']'))
            {
                s = s[1..].Trim();
            }
            else
            {
                break;
            }
        }

        return s.Trim(' ', ',', ';', ':', '-');
    }

    /// <summary>
    /// Case- and diacritic-insensitive identity for an individual artist name, so "Michael Bublé"
    /// and "michael buble" land on the same artist page.
    /// </summary>
    public static string ArtistKey(string name)
    {
        var normalized = CleanName(name).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string([.. normalized.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }

    public static string Serialize(IEnumerable<string> artists) =>
        string.Join(Delimiter, artists.Select(CleanName).Where(a => a.Length > 0));

    public static IReadOnlyList<string> Deserialize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split(Delimiter).Select(CleanName).Where(a => a.Length > 0)];

    private static bool IsProtectedAct(string name) =>
        ProtectedActs.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsFiller(string name) => Fillers.Contains(name);

    private static List<string> Words(string text) =>
        [.. CleanName(text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', '\'', '"', '(', ')'))
            .Where(w => w.Length > 0)];

    /// <summary>
    /// "His Orchestra", "all his Stars", "Orchestra & Singers", "the Orchestra".
    /// </summary>
    private static bool IsBackingGroup(string text)
    {
        var words = Words(text);
        if (words.Count > 0 && string.Equals(words[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            words.RemoveAt(0);
        }

        if (words.Count == 0)
        {
            return false;
        }

        return Possessives.Contains(words[0]) ||
            words.Any(GenericEnsembleWords.Contains) &&
            words.All(w => GenericEnsembleWords.Contains(w) || Articles.Contains(w) || JoinWords.Contains(w));
    }

    private static IEnumerable<string> TitleFeaturedClauses(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            yield break;
        }

        var found = false;
        foreach (Match m in TitleBracketFeaturing.Matches(title))
        {
            found = true;
            yield return m.Groups["names"].Value;
        }

        if (found)
        {
            yield break;
        }

        var bare = TitleBareFeaturing.Match(title);
        if (bare.Success)
        {
            yield return bare.Groups["names"].Value;
        }
    }

    private static string FirstWord(string text)
    {
        var trimmed = text.TrimStart();
        var end = trimmed.IndexOf(' ');
        return end < 0 ? trimmed : trimmed[..end];
    }

    private sealed class SplitContext(IArtistKnowledge knowledge)
    {
        private readonly List<string> _masks = [];

        public List<string> Rules { get; } = [];
        public List<string> Unresolved { get; } = [];

        public void AddRule(string rule) => Rules.Add(rule);

        /// <summary>
        /// Replaces protected act names embedded in a longer credit with single opaque tokens so
        /// separators inside them are never considered ("Earth, Wind &amp; Fire with The Emotions").
        /// </summary>
        public string Mask(string text)
        {
            foreach (var act in ProtectedActs)
            {
                var index = text.IndexOf(act, StringComparison.OrdinalIgnoreCase);
                while (index >= 0)
                {
                    var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                    var afterIndex = index + act.Length;
                    var after = afterIndex == text.Length || !char.IsLetterOrDigit(text[afterIndex]);
                    if (before && after)
                    {
                        var token = MaskToken(_masks.Count);
                        _masks.Add(text.Substring(index, act.Length));
                        text = string.Concat(text.AsSpan(0, index), token, text.AsSpan(afterIndex));
                        index = text.IndexOf(act, index + token.Length, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        index = text.IndexOf(act, index + 1, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return text;
        }

        public string Unmask(string text)
        {
            for (var i = 0; i < _masks.Count; i++)
            {
                text = text.Replace(MaskToken(i), _masks[i]);
            }

            return text;
        }

        private static string MaskToken(int index) => $"{MaskStart}{index}{MaskStart}";

        private bool IsKnown(string name)
        {
            // A whole protected act is as good as known
            var clean = CleanName(name);
            if (clean.Length > 2 && clean[0] == MaskStart && clean[^1] == MaskStart &&
                clean.Count(c => c == MaskStart) == 2)
            {
                return true;
            }

            return knowledge != null && knowledge.IsKnownArtist(CleanName(Unmask(name)));
        }

        public List<string> SplitSegment(string segment, bool listContext)
        {
            segment = CleanName(segment);
            if (listContext)
            {
                segment = CleanName(RolePrefix.Replace(segment, string.Empty));
            }

            if (segment.Length == 0)
            {
                return [];
            }

            if (IsProtectedAct(Unmask(segment)))
            {
                return [segment];
            }

            if (segment.Contains(';'))
            {
                AddRule("semicolon");
                var hasColon = segment.Contains(": ");
                return
                [
                    .. segment.Split(';')
                        .SelectMany(p => hasColon ? p.Split(": ") : [p])
                        .SelectMany(p => SplitSegment(p, true))
                ];
            }

            if (SpacedSlash.IsMatch(segment))
            {
                AddRule("slash");
                return [.. SpacedSlash.Split(segment).SelectMany(p => SplitSegment(p, true))];
            }

            var commaParts = SplitCommaList(segment);
            if (commaParts != null)
            {
                // Single unknown words ("Earth, Wind & Fire") and lowercase articles
                // ("Tyler, the Creator") mean an act name, except inside a featured list where
                // commas are almost always real list separators.
                if (commaParts.All(p =>
                        (listContext || !IsUnknownSingleWord(p)) &&
                        !(Articles.Contains(FirstWord(p)) && char.IsLower(FirstWord(p)[0]))))
                {
                    AddRule("comma-list");
                    return [.. commaParts.SelectMany(p => SplitSegment(p, true))];
                }

                Unresolved.Add("comma");
                return [segment];
            }

            return SplitAmbiguous(segment, listContext);
        }

        private bool IsUnknownSingleWord(string part)
        {
            var clean = CleanName(part);
            return !clean.Contains(' ') && !IsKnown(clean);
        }

        /// <summary>
        /// "A, B &amp; C" / "A, B, and C" / "A, B" -> parts, or null if this isn't a comma list.
        /// Name suffixes ("Sammy Davis, Jr.") and digit groups ("10,000") don't count as commas.
        /// </summary>
        private static List<string> SplitCommaList(string segment)
        {
            const char suffixComma = '\uE100';
            var protectedSegment = Suffix.Replace(segment, m => suffixComma + m.Value[1..]);
            if (!protectedSegment.Contains(", "))
            {
                return null;
            }

            var parts = protectedSegment.Split(", ")
                .Select(p => LeadingConjunction.Replace(p.Trim(), string.Empty))
                .Where(p => p.Length > 0 && !Fillers.Contains(p))
                .ToList();

            // "A, B & His Orchestra" / "A, B & The News": leave the last join for SplitAmbiguous
            var last = FinalConjunction.Match(parts[^1]);
            if (last.Success && !IsBackingGroup(last.Groups["tail"].Value) &&
                !IsActNounJoin(last.Groups["tail"].Value) &&
                !Articles.Contains(FirstWord(last.Groups["tail"].Value)))
            {
                parts[^1] = last.Groups["head"].Value;
                parts.Add(last.Groups["tail"].Value);
            }

            return parts.Count < 2
                ? null
                : [.. parts.Select(p => p.Replace(suffixComma, ','))];
        }

        private List<string> SplitAmbiguous(string segment, bool listContext)
        {
            // Joins inside brackets are part of an alias: "BnB (Blanco & Black)"
            var matches = AmbiguousSeparator.Matches(segment)
                .Where(m => !IsInsideBrackets(segment, m.Index))
                .ToList();
            if (matches.Count == 0)
            {
                return [segment];
            }

            // A leader and their backing group: "Duke Ellington and His Orchestra" -> Duke Ellington,
            // "Andy Ross with His Orchestra & Singers" -> Andy Ross. Anything credited after the
            // group ("Teddy Wilson And His Orchestra With Billie Holliday") is split on its own.
            foreach (var m in matches)
            {
                var leader = segment[..m.Index];
                var (group, tail) = BackingGroupAndTail(segment[(m.Index + m.Length)..]);
                if (CleanName(leader).Length == 0 || !IsBackingGroup(group))
                {
                    continue;
                }

                AddRule("leader");
                var leaders = SplitAmbiguous(LeadingGroupLabel.Replace(CleanName(leader), string.Empty), listContext);
                return tail == null ? leaders : [.. leaders, .. SplitSegment(tail, listContext)];
            }

            // A named ensemble joined with another full name: "London Symphony Orchestra and Árpád
            // Joó", "Peggy Lee With The Benny Goodman Orchestra"
            {
                var m = matches[0];
                var left = segment[..m.Index];
                var right = segment[(m.Index + m.Length)..];
                var rightHead = FirstJoinPart(right);
                if (IsNamedEnsemble(left) && IsMultiWord(rightHead) && !IsActNounJoin(right) ||
                    IsNamedEnsemble(rightHead) && IsMultiWord(left))
                {
                    AddRule("ensemble");
                    return [left, .. SplitAmbiguous(right, listContext)];
                }
            }

            // "Mumford & Sons", "Kool & The Gang", "The Mamas & The Papas"
            var candidates = matches
                .Where(m => !IsActNounJoin(segment[(m.Index + m.Length)..]) &&
                    !(IsArticleJoin(segment, m) && Articles.Contains(FirstWord(segment[..m.Index]))))
                .ToList();
            if (candidates.Count == 0)
            {
                Unresolved.Add("act-name");
                return [segment];
            }

            // Every piece is a known artist: "Bill Evans And Paul Motian And Scott LaFaro"
            var pieces = new List<string>();
            var start = 0;
            foreach (var m in candidates)
            {
                pieces.Add(segment[start..m.Index]);
                start = m.Index + m.Length;
            }
            pieces.Add(segment[start..]);

            if (pieces.All(IsKnown) && (listContext || !pieces.All(IsSingleWord)))
            {
                AddRule("evidence");
                return pieces;
            }

            var duos = 0;
            foreach (var m in candidates)
            {
                var left = segment[..m.Index];
                var right = segment[(m.Index + m.Length)..];
                var leftKnown = IsKnown(left);
                var rightKnown = IsKnown(right);
                var articleJoin = IsArticleJoin(segment, m);

                // Two single words ("Rodrigo y Gabriela", "Sonny & Cher") are almost always a duo
                // act, even when unrelated one-word artists share those names
                if (!listContext && IsSingleWord(left) && IsSingleWord(right))
                {
                    duos++;
                    continue;
                }

                // Both halves are known artists: "Dolly Parton & Kenny Rogers"
                if (leftKnown && rightKnown)
                {
                    AddRule("evidence");
                    return [left, right];
                }

                // One half is a known artist and the other is a full name rather than an act
                // fragment: "Lisa Loeb & Nine Stories", "Bob Marley & The Wailers" (an article
                // join needs the leader to be the known one). Both halves must be multi-word:
                // one-word names ("Fitz and The Tantrums", "Benny y Erik Sasha") collide with
                // unrelated artists too often to count as evidence.
                if (IsMultiWord(left) && IsMultiWord(right) &&
                    (leftKnown && !AmbiguousSeparator.IsMatch(right) ||
                     rightKnown && !articleJoin && !AmbiguousSeparator.IsMatch(left)))
                {
                    AddRule("evidence-partial");
                    return [left, right];
                }

                // Inside a featured list, a side doesn't need evidence as long as it looks like a
                // full name ("feat. Owl City and Carly Rae Jepsen", "feat. Shaggy & Alex Sensation")
                if (listContext && !articleJoin && (leftKnown || LooksLikeName(left)) &&
                    (rightKnown || LooksLikeName(right)))
                {
                    AddRule(leftKnown || rightKnown ? "evidence-partial" : "list-names");
                    return [left, right];
                }
            }

            Unresolved.Add(duos == candidates.Count ? "duo" : candidates[0].Groups["sep"].Value.ToLowerInvariant());
            return [segment];
        }

        private static bool IsSingleWord(string text) => !CleanName(text).Contains(' ');

        private static bool IsArticleJoin(string segment, Match separator) =>
            ArticleSensitiveSeparators.Contains(separator.Groups["sep"].Value) &&
            Articles.Contains(FirstWord(segment[(separator.Index + separator.Length)..]));

        private static bool IsInsideBrackets(string text, int index)
        {
            var before = text[..index];
            return before.Count(c => c == '(') > before.Count(c => c == ')') ||
                before.Count(c => c == '[') > before.Count(c => c == ']');
        }

        private static bool IsMultiWord(string text) => !IsSingleWord(text);

        private static bool LooksLikeName(string text) =>
            IsMultiWord(text) && !Articles.Contains(FirstWord(text)) && !AmbiguousSeparator.IsMatch(text);

        /// <summary>
        /// Splits the text after a join into the part that could be a backing group and whatever is
        /// credited after it: "His Orchestra With Billie Holliday" -> ("His Orchestra", "Billie Holliday").
        /// </summary>
        private static (string Group, string Tail) BackingGroupAndTail(string text)
        {
            foreach (Match m in AmbiguousSeparator.Matches(text))
            {
                var rest = text[(m.Index + m.Length)..];
                if (!IsBackingGroup(FirstJoinPart(rest)))
                {
                    return (text[..m.Index], rest);
                }
            }
            return (text, null);
        }

        private static string FirstJoinPart(string text)
        {
            var m = AmbiguousSeparator.Match(text);
            return m.Success ? text[..m.Index] : text;
        }

        private static bool IsNamedEnsemble(string text)
        {
            var words = Words(text);
            return words.Count >= 2 && !AmbiguousSeparator.IsMatch(text) && !IsBackingGroup(text) &&
                words.Any(EnsembleWords.Contains);
        }

        private static bool IsActNounJoin(string textAfterJoin)
        {
            var words = Words(textAfterJoin);
            var first = words.Count > 1 && Articles.Contains(words[0]) ? words[1] : words.FirstOrDefault();
            return first != null && ActNouns.Contains(first);
        }
    }
}
