using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DanceLibrary;
using Newtonsoft.Json;

namespace m4dModels
{
    public enum DanceCategoryType
    {
        Both,
        American,
        International
    };

    [JsonObject(MemberSerialization.OptIn)]
    public class CompetitionDance
    {
        public CompetitionDance(string group, string category, string id, int order, DanceMusicService dms)
        {
            Group = group;
            Category = category;
            DanceId = id;
            Order = order;
            DanceStats = DanceStatsManager.FromId(id,dms);
            DanceStats.AddCompetitionDance(this);
        }
        public DanceStats DanceStats { get; }

        [JsonProperty]
        public string DanceId { get; }

        [JsonProperty]
        public string Group { get; }
        [JsonProperty]
        public string Category { get; }

        [JsonProperty]
        public int Order { get; }

        public DanceInstance SpecificDance => Dances.Instance.DanceFromId(DanceId) as DanceInstance;

    }
    public class DanceCategory
    {
        public string Name { get; }
        public string Group { get; }
        public string CanonicalName => BuildCanonicalName(Name);
        public IReadOnlyList<CompetitionDance> Round => _round;
        public IReadOnlyList<CompetitionDance> Extras => _extra;

        internal DanceCategory(DanceMusicService dms, string group, string name, IEnumerable<string> round, IEnumerable<string> extras = null)
        {
            Group = group;
            Name = name;
            _round= new List<CompetitionDance>();
            foreach (var d in round)
            {
                _round.Add(new CompetitionDance(group, name, d,_round.Count,dms));
            }
            if (extras == null) return;

            _extra = new List<CompetitionDance>();
            foreach (var d in extras)
            {
                _extra.Add(new CompetitionDance(group, name, d, -1, dms));
            }
        }

        private readonly List<CompetitionDance> _round;
        private readonly List<CompetitionDance> _extra;

        public static string BuildCanonicalName(string name)
        {
            var sb = new StringBuilder();
            foreach (var c in name.Where(char.IsLetter))
            {
                sb.Append(char.ToLower(c));
            }

            return sb.ToString();
        }
    }

    public class DanceCategories
    {

        public DanceCategory FromName(string name)
        {
            DanceCategory cat;
            return _categories.TryGetValue(DanceCategory.BuildCanonicalName(name), out cat) ? cat : null;
        }

        public IEnumerable<DanceCategory> GetGroup(string group)
        {
            if (!string.Equals(group, Ballroom, StringComparison.OrdinalIgnoreCase)) return null;

            // For now the only group is "ballroom", but this seems a worthwhile abstraction
            return new List<DanceCategory>
            {
                FromName(Standard),
                FromName(Latin),
                FromName(Smooth),
                FromName(Rhythm)
            };
        }

        public int CountGroups => 1;

        public int CountCategories => _categories.Count;

        private void AddCategory(DanceMusicService dms, string group, string name, IEnumerable<string> round, IEnumerable<string> extras = null)
        {
            var cat = new DanceCategory(dms, group, name, round, extras);
            _categories[DanceCategory.BuildCanonicalName(name)] = cat;
        }

        public void Initialize(DanceMusicService dms)
        {
            if (_categories.Count > 0) return;

            AddCategory(dms, Ballroom, Standard, new[] { "SWZI", "TGOI", "VWZI", "SFTI", "QSTI" });
            AddCategory(dms, Ballroom, Latin, new[] { "CHAI", "SMBI", "RMBI", "PDLI", "JIVI" });
            AddCategory(dms, Ballroom, Smooth, new[] { "SWZA", "TGOA", "SFTA", "VWZA" }, new[] { "PBDA" });
            AddCategory(dms, Ballroom, Rhythm, new[] { "CHAA", "RMBA", "ECSA", "BOLA", "MBOA" }, new[] { "HSTA", "MRGA", "PDLA", "PLKA", "SMBA", "WCSA" });
        }

        public void Clear()
        {
            _categories.Clear();
        }

        public const string Standard = "International Standard";
        public const string Latin = "International Latin";
        public const string Smooth = "American Smooth";
        public const string Rhythm = "American Rhythm";
        public const string Ballroom = "Ballroom";

        private readonly Dictionary<string, DanceCategory> _categories = new Dictionary<string, DanceCategory>();
    }
}