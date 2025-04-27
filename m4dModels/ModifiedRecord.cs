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
            ApplicationUser.BuildDecoratedName(UserName, IsPseudo);

        [NotMapped]
        [JsonIgnore]
        public ApplicationUser ApplicationUser =>
            new(UserName, IsPseudo);

        public static bool? ParseLike(string likeString)
        {
            return bool.TryParse(likeString, out var like) ? like : null;
        }
    }
}
