using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DanceLibrary;

public class CompetitionCategory
{
    public const string Standard = "International Standard";
    public const string Latin = "International Latin";
    public const string Smooth = "American Smooth";
    public const string Rhythm = "American Rhythm";
    public const string Ballroom = "Ballroom";

    private static readonly Dictionary<string, List<CompetitionCategory>> s_mapGroups =
        new();

    private static readonly Dictionary<string, CompetitionCategory> s_mapCategories =
        new();

    private readonly List<DanceInstance> _extra = new();

    private readonly List<DanceInstance> _round = new();

    public string Name { get; private set; }
    public string Group { get; private set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public DanceCategoryType CategoryType => Name.StartsWith("International")
        ? DanceCategoryType.International
        : DanceCategoryType.American;

    public string CanonicalName => BuildCanonicalName(Name);
    public string FullRoundName => $"{Name} {(Round.Count == 4 ? "four" : "five")} dance round";

    public IList<DanceInstance> Round => _round;
    public IList<DanceInstance> Extras => _extra;

    internal static void RegisterDanceInstance(DanceInstance dance)
    {
        if (string.IsNullOrWhiteSpace(dance.CompetitionGroup))
        {
            return;
        }

        var name = BuildCanonicalName(dance.Style);
        var category = GetCategory(name);
        if (category == null)
        {
            var group = (List<CompetitionCategory>)GetCategoryList(dance.CompetitionGroup);
            category = new CompetitionCategory
                { Name = dance.Style, Group = dance.CompetitionGroup };
            group.Add(category);
            s_mapCategories[name] = category;
        }

        if (dance.CompetitionOrder > 0)
        {
            category._round.Add(dance);
            category._round.Sort(
                (c1, c2) =>
                    c1.CompetitionOrder.CompareTo(c2.CompetitionOrder));
        }
        else
        {
            category._extra.Add(dance);
        }
    }

    internal static void Reset()
    {
        s_mapGroups.Clear();
        s_mapCategories.Clear();
    }

    internal static IEnumerable<CompetitionCategory> GetCategoryList(string name)
    {
        if (s_mapGroups.TryGetValue(name, out var categories))
        {
            return categories;
        }

        categories = new List<CompetitionCategory>();
        s_mapGroups[name] = categories;
        return categories;
    }

    public static CompetitionCategory GetCategory(string name)
    {
        return s_mapCategories.TryGetValue(BuildCanonicalName(name), out var category)
            ? category
            : null;
    }

    public static string BuildCanonicalName(string name)
    {
        return name.ToLowerInvariant().Replace(' ', '-');
    }
}
