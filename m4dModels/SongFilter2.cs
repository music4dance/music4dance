using Microsoft.SqlServer.Server;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace m4dModels;

internal class SongFilter2 : SongFilter
{
    private class TagClass
    {
        public TagClass(string name, bool isSongTag = true, bool isDanceTag = true)
        {
            Name = name;
            IsSongTag = isSongTag;
            IsDanceTag = isDanceTag;
        }
        public string Name { get; set; }
        public bool IsSongTag { get; set; }
        public bool IsDanceTag { get; set; }
    }

    private static readonly Dictionary<string, TagClass> s_tagClasses =
        new()
        {
                { "Music", new TagClass("Genre", isDanceTag: false ) },
                { "Style", new TagClass("Style", isSongTag: false) },
                { "Tempo", new TagClass("Tempo" )},
                { "Other", new TagClass("Other") }
        };

    internal SongFilter2(string filter = null) : base(filter)
    {
    }

    internal SongFilter2(RawSearch rawSearch, string action) : base(rawSearch, action)
    {
    }

    public override SongFilter Clone()
    {
        return new SongFilter2(ToString());
    }

    public override DanceQuery DanceQuery => new DanceQuery2(IsRaw ? null : Dances);

    public override SongFilter CreateCustomSearchFilter(string name = "holiday", string dance = null, int page = 1)
    {
        var holidayFilter = name.ToLowerInvariant() switch
        {
            "halloween" => "OtherTags/any(t: t eq 'Halloween')",
            "holiday" or "christmas" => "(OtherTags/any(t: t eq 'Holiday') or GenreTags/any(t: t eq 'Christmas' or t eq 'Holiday')) and OtherTags/all(t: t ne 'Halloween')",
            "broadway" => "GenreTags/any(t: t eq 'Broadway') or GenreTags/any(t: t eq 'Show Tunes') or GenreTags/any(t: t eq 'Musicals') or GenreTags/any(t: t eq 'Broadway And Vocal')",
            _ => throw new Exception($"Unknown holiday: {name}"),
        };
        string danceFilter = null;
        string danceSort = null;
        if (string.IsNullOrWhiteSpace(dance))
        {
            danceSort = "dance_ALL/Votes desc";
        }
        else
        {
            var d = DanceLibrary.Dances.Instance.DanceFromName(dance);
            if (d != null)
            {
                danceFilter = $"DanceTags/any(t: t eq '{dance}')";
                danceSort = $"dance_{d.Id}/Votes desc";
            }
        }

        var odata = string.IsNullOrWhiteSpace(dance)
            ? holidayFilter
            : $"{danceFilter} and ({holidayFilter})";

        return new SongFilter2(
            new RawSearch
            {
                ODataFilter = odata,
                SortFields = danceSort,
                Page = page,
                Flags = danceFilter == null ? "" : "singleDance"
            },
            "customsearch"
        );
    }

    public override string GetTagFilter(DanceMusicCoreService dms)
    {
        var tags = new TagList(Tags);

        if (tags.IsEmpty)
        {
            return null;
        }

        var tlInclude = new TagList(Tags);
        var tlExclude = new TagList();

        if (tlInclude.IsQualified)
        {
            var temp = tlInclude;
            tlInclude = temp.ExtractAdd();
            tlExclude = temp.ExtractRemove();
        }

        // We're accepting either a straight include list of tags or a qualified list (+/- for include/exlude)
        // TODO: For now this is going to be explicit (i&i&!e*!e) - do we need a stronger expression syntax at this level
        //  or can we do some kind of top level OR of queries?

        var rInclude = new TagList(dms.GetTagRings(tlInclude).Select(tt => tt.Key));
        var rExclude = new TagList(dms.GetTagRings(tlExclude).Select(tt => tt.Key));

        var sb = new StringBuilder();

        foreach (var tp in s_tagClasses)
        {
            var tagClass = tp.Value;
            HandleFilterClass(sb, rInclude, tp.Key, tagClass.Name, 
                tagClass.IsSongTag ? "{0}Tags/any(t: t eq '{1}')" : null,
                tagClass.IsDanceTag ? "dance_ALL/{0}Tags/any(t: t eq '{1}')" : null);
            HandleFilterClass(sb, rExclude, tp.Key, tagClass.Name,
                tagClass.IsSongTag ? "{0}Tags/all(t: t ne '{1}')" : null,
                tagClass.IsDanceTag ? "dance_ALL/{0}Tags/all(t: t ne '{1}')" : null);
        }

        return sb.ToString();
    }

    private static void HandleFilterClass(
        StringBuilder sb, TagList tags, string tagClass, string tagName, string songFormat, string danceFormat)
    {
        var filtered = tags.Filter(tagClass);
        if (filtered.IsEmpty)
        {
            return;
        }

        foreach (var t in filtered.StripType())
        {
            if (sb.Length > 0)
            {
                sb.Append(" and ");
            }

            var tt = t.Replace(@"'", @"''");

            if (songFormat != null && danceFormat != null)
            {
                sb.Append("(");
                sb.AppendFormat(songFormat, tagName, tt);
                sb.Append(" or ");
                sb.AppendFormat(danceFormat, tagName, tt);
                sb.Append(")");
            }
            else if (songFormat != null)
            {
                sb.AppendFormat(songFormat, tagName, tt);
            }
            else if (danceFormat != null)
            {
                sb.AppendFormat(danceFormat, tagName, tt);
            }
        }
    }
}

