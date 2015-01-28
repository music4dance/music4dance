using System;

namespace m4dModels
{
    public class Tag
    {
        public DateTime Modified { get; set; }
        public string Id { get; set; }
        public TagList Tags { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }
}
