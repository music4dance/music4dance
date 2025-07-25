﻿using System.Collections.Generic;
using System.Linq;

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
        [];

    private static readonly Dictionary<string, CompetitionCategory> s_mapCategories =
        [];

    private readonly List<DanceInstance> _extra = [];

    private readonly List<DanceInstance> _round = [];

    public string Name { get; private set; }
    public string Group { get; private set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public DanceCategoryType CategoryType => Name.StartsWith("International")
        ? DanceCategoryType.International
        : DanceCategoryType.American;

    [JsonIgnore]
    public string CanonicalName => BuildCanonicalName(Name);
    [JsonIgnore]
    public string FullRoundName => $"{Name} {(_round.Count == 4 ? "four" : "five")} dance round";

    public IReadOnlyList<string> Round => [.. _round.Select(d => d.Id)];
    public IReadOnlyList<string> Extras => [.. _extra.Select(d => d.Id)];

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

        categories = [];
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
