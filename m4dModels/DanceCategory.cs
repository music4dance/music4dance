using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DanceLibrary;

namespace m4dModels
{
    public enum DanceCategoryType
    {
        Both,
        American,
        International
    };

    public struct CompetitionDance
    {
        public CompetitionDance(string id, DanceMusicService dms) : this()
        {
            DanceStats = DanceStatsManager.FromId(id,dms);
            SpecificDance = Dances.Instance.DanceFromId(id) as DanceInstance;
            DanceStats.AddCompetitionDance(this);
        }
        public DanceStats DanceStats { get; private set; }
        public DanceInstance SpecificDance { get; private set; }
    }
    public class DanceCategory
    {
        public string Name { get; }
        public string CanonicalName => BuildCanonicalName(Name);
        public IReadOnlyList<CompetitionDance> Round => _round;
        public IReadOnlyList<CompetitionDance> Extras => _extra;

        internal DanceCategory(DanceMusicService dms, string name, IEnumerable<string> round, IEnumerable<string> extras = null)
        {
            Name = name;
            _round= new List<CompetitionDance>();
            foreach (var d in round)
            {
                _round.Add(new CompetitionDance(d,dms));
            }
            if (extras == null) return;

            _extra = new List<CompetitionDance>();
            foreach (var d in extras)
            {
                _extra.Add(new CompetitionDance(d, dms));
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

        private void AddCategory(DanceMusicService dms, string name, IEnumerable<string> round, IEnumerable<string> extras = null)
        {
            var cat = new DanceCategory(dms, name, round, extras);
            _categories[DanceCategory.BuildCanonicalName(name)] = cat;
        }

        public void Initialize(DanceMusicService dms)
        {
            if (_categories.Count > 0) return;

            AddCategory(dms, Standard, new[] { "SWZI", "TGOI", "VWZI", "SFTI", "QSTI" });
            AddCategory(dms, Latin, new[] { "CHAI", "SMBI", "RMBI", "PDLI", "JIVI" });
            AddCategory(dms, Smooth, new[] { "SWZA", "TGOA", "SFTA", "VWZA" }, new[] { "PBDA" });
            AddCategory(dms, Rhythm, new[] { "CHAA", "RMBA", "ECSA", "BOLA", "MBOA" }, new[] { "HSTA", "MRGA", "PDLA", "PLKA", "SMBA", "WCSA" });
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