
namespace m4d.Models
{
    public class ModifiedRecord
    {
        public string ApplicationUserId { get; set; }
        public virtual ApplicationUser ApplicationUser { get; set; }

        public int SongId { get; set; }
        public virtual Song Song { get; set; }
    }
}