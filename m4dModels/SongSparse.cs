using System;
using System.Collections.Generic;
using AutoMapper;

namespace m4dModels
{
    public class SongProfile : Profile
    {
        public SongProfile()
        {
            CreateMap<Song, SongSparse>()
                .ForMember(dest => dest.Tags,
            opt => opt.MapFrom(src => src.TagSummary.Tags))
                .ForMember(dest => dest.Danceability,
                    opt => opt.MapFrom(y => y.Danceability == null || float.IsNaN(y.Danceability.Value) ? null : y.Danceability))
                .ForMember(dest => dest.Valence,
                    opt => opt.MapFrom(y => y.Valence == null || float.IsNaN(y.Valence.Value) ? null : y.Valence))
                .ForMember(dest => dest.Energy,
                opt => opt.MapFrom(y => y.Energy == null || float.IsNaN(y.Energy.Value) ? null : y.Energy));

            CreateMap<DanceRating, DanceRatingSparse>();
            CreateMap<AlbumDetails, AlbumDetailsSparse>();
        }
    }

    public class SongSparse
    {
        public Guid SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public int? Length { get; set; }
        public string Sample { get; set; }
        public float? Danceability { get; set; }
        public float? Energy { get; set; }
        public float? Valence { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public List<TagModel> Tags { get; set; }
        public List<DanceRatingSparse> DanceRatings { get;set; }
        public List<ModifiedRecord> ModifiedBy { get; set; }
        public List<AlbumDetailsSparse> Albums { get; set; }
    }

    public class DanceRatingSparse
    {
        public string DanceId { get; set; }
        public int Weight { get; set; }
    }

    public class AlbumDetailsSparse
    {
        public string Name { get; set; }
        public string Publisher { get; set; }
        public int? Track { get; set; }
        public Dictionary<string, string> Purchase { get; set; }
    }

    public class TagModel
    {
        public string Value { get; set; }
        public string Category { get; set; }
        public int Count { get; set; }
    }
}
