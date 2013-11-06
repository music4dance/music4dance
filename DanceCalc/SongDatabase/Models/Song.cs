using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace SongDatabase.Models
{    public class Song : DbObject
    {
        public int SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public int? Track { get; set; }
        public int? Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public int TitleHash { get; set; }
        // Semi-colon separated purchase info of the form XX=YYYYYY (XX is service/type and YYYYY is id)
        public string Purchase { get; set; }
        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<UserProfile> ModifiedBy { get; set; }
        public virtual ICollection<SongProperty> SongProperties { get; set; }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},Title={1},Album={2},Artist={3}",SongId,Title,Album,Artist);
            Debug.WriteLine(output);
            if (ModifiedBy != null)
            {
                foreach (UserProfile user in ModifiedBy)
                {
                    Debug.Write("\t");
                    user.Dump();
                }
            }
        }

        public static Song GetNullSong()
        {
            return new Song();
        }
    }
}