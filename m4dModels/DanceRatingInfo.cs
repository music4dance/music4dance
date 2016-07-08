using System;
using System.Runtime.Serialization;

namespace m4dModels
{
    [DataContract]
    public class DanceRatingInfo : DanceRating
    {
        public DanceRatingInfo()
        {
        }

        private DanceRatingInfo(DanceRating dr)
        {
            DanceId = dr.DanceId;
            SongId = dr.SongId;
            Weight = dr.Weight;
            TagSummary = dr.TagSummary;
        }

        public DanceRatingInfo(DanceRating dr, ApplicationUser user, DanceMusicService dms) : this(dr)
        {
            Init(DanceStatsManager.GetInstance(dms));

            if (user != null)
            {
                SetCurrentUserTags(user, dms);
            }
        }

        public DanceRatingInfo(DanceRating dr, TagList userTags, DanceStatsInstance stats) : this(dr)
        {
            Init(stats);
            _currentUserTags = userTags;
        }

        private void Init(DanceStatsInstance stats)
        {
            var sc = stats.FromId(DanceId);

            if (sc != null)
            {
                DanceName = sc.DanceName;
                Max = sc.MaxWeight;
            }

            Badge = stats.GetRatingBadge(DanceId, Weight);
        }

        [DataMember]
        public string DanceName { get; set; }
        [DataMember]
        public int Max { get; set; }
        [DataMember]
        public string Badge { get; set; }
        [DataMember]
        public TagList CurrentUserTags
        {
            get { return _currentUserTags; }
            set { throw new NotImplementedException("Shouldn't hit the setter for this."); }
        }
        public void SetCurrentUserTags(ApplicationUser user, DanceMusicService dms)
        {
            _currentUserTags = UserTags(user, dms);
        }
        private TagList _currentUserTags;

    }
}