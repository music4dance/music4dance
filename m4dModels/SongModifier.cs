using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace m4dModels
{
    public class SongModifier
    {
        public List<string> ExcludeUsers { get; set; }
        public List<PropertyModifier> Properties { get; set; }

        public static SongModifier Build(string modInfo)
        {
            var modifier = JsonConvert.DeserializeObject<SongModifier>(modInfo);
            if (modifier == null)
            {
                throw new ArgumentNullException(modInfo);
            }

            var ratingTags = modifier.Properties.Where(
                    p => p.Action == PropertyAction.ReplaceValue && p.Name == Song.DanceRatingField)
                .SelectMany(
                    p => new[] { BuildTagRating(p, "+"), BuildTagRating(p, "-") });

            return new SongModifier
            {
                ExcludeUsers = modifier.ExcludeUsers,
                Properties = modifier.Properties.Concat(ratingTags).ToList()
            };
        }

        private static PropertyModifier BuildTagRating(PropertyModifier modifier, string type)
        {
            return new PropertyModifier
            {
                Action = PropertyAction.ReplaceName, Name = $"Tag{type}:{modifier.Value}",
                Replace = $"Tag{type}:{modifier.Replace}"
            };
        }
    }
}
