using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace m4dModels
{
    public sealed class Search
    {
        public void Update(ApplicationUser user, string name, string query, bool favorite, int count, DateTime created,
            DateTime modified)
        {
            ApplicationUser = user;
            Name = name;
            Query = query;
            Favorite = favorite;
            Count = count;
            Created = created;
            Modified = modified;
        }

        public long Id { get; set; }
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }

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
