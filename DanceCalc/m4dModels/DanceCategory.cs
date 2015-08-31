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
            SongCount = SongCounts.GetDanceMap(dms)[id.Substring(0,3)];
            SpecificDance = Dances.Instance.DanceDictionary[id] as DanceInstance;
            SongCount.AddCompetitionDance(this);
        }
        public SongCounts SongCount { get; private set; }
        public DanceInstance SpecificDance { get; private set; }
    }
    public class DanceCategory
    {
        public string Name { get; private set; }
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
        static public DanceCategories GetDanceCategories(DanceMusicService dms)
        {
            lock (Instance)
            {
                Instance.Initialize(dms);
                return Instance;
            }
        }

        static public void ClearCache()
        {
            lock (Instance)
            {
                Instance._categories.Clear();
            }
        }

        public DanceCategory FromName(string name)
        {
            DanceCategory cat;
            return _categories.TryGetValue(DanceCategory.BuildCanonicalName(name), out cat) ? cat : null;
        }
        private void AddCategory(DanceMusicService dms, string name, IEnumerable<string> round, IEnumerable<string> extras = null)
        {
            var cat = new DanceCategory(dms, name, round, extras);
            _categories[DanceCategory.BuildCanonicalName(name)] = cat;
        }

        private DanceCategories()
        {
        }

        private void Initialize(DanceMusicService dms)
        {
            if (_categories.Count > 0) return;

            AddCategory(dms,"International Standard", new[] { "SWZI", "TGOI", "VWZI", "SFTI", "QSTI" });
            AddCategory(dms, "International Latin", new[] { "CHAI", "SMBI", "RMBI", "PDLI", "JIVI" });
            AddCategory(dms, "American Smooth", new[] { "SWZA", "TGOA", "SFTA", "VWZA" }, new[] { "PBDA" });
            AddCategory(dms, "American Rhythm", new[] { "CHAA", "RMBA", "ECSA", "BOLA", "MBOA" }, new[] { "HSTA", "MRGA", "PDLA", "PLKA", "SMBA", "WCSA" });
        }

        private readonly Dictionary<string, DanceCategory> _categories = new Dictionary<string, DanceCategory>();

        private static readonly DanceCategories Instance = new DanceCategories();
    }
}