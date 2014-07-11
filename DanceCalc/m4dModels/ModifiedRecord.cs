using System;

namespace m4dModels
{
    public class ModifiedRecord
    {
        public string ApplicationUserId { get; set; }
        public virtual ApplicationUser ApplicationUser { get; set; }

        public Guid SongId { get; set; }
        public virtual Song Song { get; set; }
    }
}