using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace m4dModels
{
    public enum PlayListType
    {
        Undefined,
        Music4Dance,
        SongsFromSpotify,
        SpotifyFromSearch
    }

    public class PlayList
    {
        public string User { get; set; }
        public PlayListType Type { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Data1 { get; set; }
        public string Data2 { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Updated { get; set; }
        public bool Deleted { get; set; }

        public string Data1Name => GetData1Name(Type);

        public static string GetData1Name(PlayListType type)
        {
            return type switch
            {
                PlayListType.SongsFromSpotify => "Tags",
                PlayListType.SpotifyFromSearch => "Search",
                _ => "Data1",
            };
        }

        public string Data2Name => GetData2Name(Type);

        public static string GetData2Name(PlayListType type)
        {
            switch (type)
            {
                case PlayListType.SongsFromSpotify: return "SongIds";
                default: return "Data2";
            }
        }

        /* SongsFromSpotify: The following members are valid when Type == SongsFromSpotify */

        [NotMapped]
        public string Tags
        {
            get => Data1;
            set => Data1 = value;
        }

        [NotMapped]
        public string SongIds
        {
            get => Data2;
            set => Data2 = value;
        }

        public IEnumerable<string> SongIdList => string.IsNullOrEmpty(SongIds)
            ? null
            : SongIds.Split(['|'], StringSplitOptions.RemoveEmptyEntries);

        public bool AddSongs(IEnumerable<string> songIds)
        {
            var existing = string.IsNullOrEmpty(SongIds)
                ? new List<string>()
                : [..SongIdList];

            var initial = existing.Count;
            foreach (var id in songIds.Where(id => !existing.Contains(id)))
            {
                existing.Add(id);
            }

            if (initial == existing.Count)
            {
                return false;
            }

            SongIds = string.Join("|", existing);
            Updated = DateTime.Now;
            return true;
        }

        /* SpotifyFromSearch: The following members are valid when Type == SpotifyFromSearch */
        [NotMapped]
        public string Search
        {
            get => Data1;
            set => Data1 = value;
        }

        [NotMapped]
        public int Count
        {
            get => int.TryParse(Data2, out var c) ? c : -1;
            set => Data2 = value.ToString();
        }
    }


    public class GenericPlaylist
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string OwnerId { get; set; }
        public string OwnerName { get; set; }
        public IList<ServiceTrack> Tracks { get; set; }
    }

    public class PlaylistMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Link { get; set; }
        public string Reference { get; set; }
        public int? Count { get; set; }
    }

    // TODO: refactor to SpotifyCreateInfo & ExportPlaylistInfo
    public class PlaylistCreateInfo
    {
        [Required(ErrorMessage = "Title is Required")]
        public string Title { get; set; }

        public string DescriptionPrefix { get; set; }

        [StringLength(225, ErrorMessage = "Description must be less than 225 characters.")]
        public string Description { get; set; }

        public string Filter { get; set; }

        public virtual int Count { get; set; }

        // User info
        public bool IsAuthenticated { get; set; }
        public bool IsPremium { get; set; }
        public SubscriptionLevel SubscriptionLevel { get; set; }
    }

    public class SpotifyCreateInfo : PlaylistCreateInfo, IValidatableObject
    {
        //[Range(5, 100, ErrorMessage = "A playlist may have between 5 and 100 songs")]
        [Display(Name = "Number of Songs")]
        public override int Count { get; set; }

        public bool CanSpotify { get; set; }

        public bool PageWarning { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Count < 5)
            {
                yield return new ValidationResult("Playlists must have at least 5 songs", [nameof(Count)]);
            }
            else if (Count > 1000)
            {
                yield return new ValidationResult("Playlists may not have more than 1000 songs", [nameof(Count)]);
            }
            else if (Count > 100)
            {
                if (SubscriptionLevel < SubscriptionLevel.Bronze)
                {
                    yield return new ValidationResult("You must have at least a bronze subscription to create a playlist of more than a hundred songs.", [nameof(Count)]);
                }
            }
        }
    }

    public class ExportInfo : PlaylistCreateInfo
    {
        [Display(Name = "Only include the dances that I searched on")]
        public bool IncludeSpecificDances { get; set; }
        public bool IsSelf { get; set; }
    }

    public class PlayListIndex
    {
        public PlayListType Type;
        public List<PlayList> PlayLists;

        public string Data1Name => PlayList.GetData1Name(Type);
        public string Data2Name => PlayList.GetData2Name(Type);

        public bool HasData1 => !string.Equals(Data1Name, "Data1");

        public bool HasData2 => !string.Equals(Data2Name, "Data2");
    }
}
