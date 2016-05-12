using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace m4dModels
{
    public class Search
    {
        public long Id { get; set; }
        public string ApplicationUserId { get; set; }
        public virtual ApplicationUser ApplicationUser { get; set; }

        public string Name { get; set; }
        public string Query { get; set; }
        public bool Favorite { get; set; }
        public int Count { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        [NotMapped]
        public SongFilter Filter => new SongFilter(Query);
    }
}
