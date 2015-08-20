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

        public DanceRatingInfo(DanceRating dr, ApplicationUser user, DanceMusicService dms)
        {
            // TODO: Make this more resilient - server appears to have gotten into a state where SongCounts weren't returning this map correctly
            var map = SongCounts.GetDanceMap(dms);

            DanceId = dr.DanceId;
            SongId = dr.SongId;
            Weight = dr.Weight;
            TagSummary = dr.TagSummary;
            var sc = map[DanceId];
            DanceName = sc.DanceName;
            Max = sc.MaxWeight;
            Badge = SongCounts.GetRatingBadge(map, DanceId, Weight);

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