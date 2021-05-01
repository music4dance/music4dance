using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace m4dModels
{
    public class ModifiedRecord
    {
        public ModifiedRecord()
        {
        }

        public ModifiedRecord(string value)
        {
            var parts = value.Split('|');
            UserName = parts[0];
            // Eventually this may be a generic flag field, but for now the
            //  only valid flag is Pseudo == "P"
            IsPseudo = parts.Length > 1 && parts[1] == "P";
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

        public bool IsPseudo { get; set; }

        [NotMapped]
        [JsonIgnore]
        public string DecoratedName =>
            ApplicationUser.BuildDecoratedName(UserName,IsPseudo);

        [NotMapped]
        [JsonIgnore]
        public ApplicationUser ApplicationUser =>
            new ApplicationUser(UserName, IsPseudo);

        [NotMapped]
        [JsonIgnore]
        public string LikeString {
            get => Like?.ToString() ?? "null";
            set => ParseLike(value);
        }

        public static bool? ParseLike(string likeString)
        {
            if (bool.TryParse(likeString, out var like))
                return like;
            return null;
        }
    }
}