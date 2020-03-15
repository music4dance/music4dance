using m4dModels;

namespace m4d.ViewModels
{
    public class LikeHelperModel
    {
        public Song Song { get; set; }
        public LikeDictionary Likes { get; set; }
        public string Image { get; set; }
        public string Modifier { get; set; }
        public string DanceId { get; set; }
        public string TipNull { get; set; }
        public string TipTrue { get; set; }
        public string TipFalse { get; set; }
    }
}
