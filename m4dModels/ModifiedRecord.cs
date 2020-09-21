using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace m4dModels
{
    public class ModifiedRecord
    {
        public ModifiedRecord()
        {
        }

        public ModifiedRecord(ModifiedRecord mod)
        {
            UserName = mod.UserName;
            Like = mod.Like;
            Owned = mod.Owned;
        }

        // This is both a boolean to indicate that the user owns the track
        //  and a hash for the filename so that in the future hopefully
        //  we can do a quick match on the user's machine
        public int? Owned { get; set; }

        public bool? Like { get; set; }

        public string UserName { get; set; }

        [NotMapped]
        [JsonIgnore]
        public string LikeString {
            get => Like?.ToString() ?? "null";
            set => ParseLike(value);
        }

        public static bool? ParseLike(string likeString)
        {
            bool like;
            if (bool.TryParse(likeString, out like))
                return like;
            return null;
        }
    }
}