using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DanceLibrary;

namespace m4dModels
{
    public class DanceStats
    {        public string DanceId { get; set; }

        public string DanceName { get; set; }
        public int SongCount { get; set; }
        public int MaxWeight { get; set; }
        public string DanceNameAndCount => $"{DanceName} ({SongCount})";
        public string BlogTag { get; set; }
        public Dance Dance { get; set; }
        public DanceStats Parent { get; set; }
        public List<DanceStats> Children { get; set; }
        public string SeoName => DanceObject.SeoFriendly(DanceName);

        public IEnumerable<SongBase> TopSongs { get; set; }

        public ICollection<ICollection<PurchaseLink>> TopSpotify { get; set; }

        public IReadOnlyList<CompetitionDance> CompetitionDances => _competitionDances;

        public void AddCompetitionDance(CompetitionDance competitionDance)
        {
            lock (this)
            {
                if (_competitionDances == null)
                {
                    _competitionDances = new List<CompetitionDance>();
                }
                _competitionDances.Add(competitionDance);
            }
        }

        private List<CompetitionDance> _competitionDances;

    }
}
