using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace m4dModels
{
    [DataContract]
    public class DanceRatingInfo : DanceRating
    {
        public DanceRatingInfo()
        {
        }

        public DanceRatingInfo(DanceRating dr, ApplicationUser user, DanceMusicService dms)
        {
            DanceId = dr.DanceId;
            SongId = dr.SongId;
            Weight = dr.Weight;
            TagSummary = dr.TagSummary;
            var stats = DanceStatsManager.GetInstance(dms);
            var sc = stats.FromId(DanceId);

            if (sc != null)
            {
                DanceName = sc.DanceName;
                Max = sc.MaxWeight;
            }

            Badge = stats.GetRatingBadge(DanceId, Weight);

            if (user != null)
            {
                SetCurrentUserTags(user, dms);
            }
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