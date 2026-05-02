using Newtonsoft.Json;

namespace m4dModels;

public class SongModifier
{
    public List<string> ExcludeUsers { get; set; }
    public List<PropertyModifier> Properties { get; set; }

    /// <summary>
    /// When set, limits property mutations to blocks whose timestamp is on or after this date.
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// When set, limits property mutations to blocks whose timestamp is on or before this date.
    /// </summary>
    public DateTime? ToDate { get; set; }

    public static SongModifier Build(string modInfo)
    {
        var modifier = JsonConvert.DeserializeObject<SongModifier>(modInfo) ?? throw new ArgumentNullException(modInfo);

        // When we change a dance rating from one dance to another, we also need to change the tags on top
        //  of it to the new dance.  Adding two extra modifiers to change TAG+:OLD to TAG+:NEW+ and TAG-:OLD to TAG-:NEW
        //  make that happen
        var ratingTags = modifier.Properties.Where(
                p => p.Action == PropertyAction.ReplaceValue && p.Name == Song.DanceRatingField)
            .SelectMany(
                p => new[] { BuildTagRating(p, "+"), BuildTagRating(p, "-") });

        return new SongModifier
        {
            ExcludeUsers = modifier.ExcludeUsers,
            Properties = [.. modifier.Properties, .. ratingTags],
            FromDate = modifier.FromDate,
            ToDate = modifier.ToDate,
        };
    }

    private static PropertyModifier BuildTagRating(PropertyModifier modifier, string type)
    {
        return new PropertyModifier
        {
            // TODO: Dance tags are currently always 3 characters, might be better to take the substring up to the "+"
            Action = PropertyAction.ReplaceName, Name = $"Tag{type}:{modifier.Value[..3]}",
            Replace = $"Tag{type}:{modifier.Replace[..3]}"
        };
    }
}
