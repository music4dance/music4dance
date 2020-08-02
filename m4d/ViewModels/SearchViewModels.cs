using System.Collections.Generic;
using AutoMapper;
using DanceLibrary;
using m4dModels;

namespace m4d.ViewModels
{
    public class TagModel
    {
        public string Value { get; set; }
        public string Category { get; set; }
        public int Count { get; set; }
    }

    public class TagProfile : Profile
    {
        public TagProfile()
        {
            CreateMap<TagCount, TagModel>()
                .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.TagValue))
                .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.TagClass));
            CreateMap<TagGroup, TagModel>();
        }
    }
    public class SearchModel
    {
        public SongFilterSparse Filter { get; set; }
        public List<DanceObject> Dances { get; set; }
        public List<TagModel> Tags { get; set; }
    }

    public class ServiceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Purchase { get; set; }
    }

    public class BonusInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? Cruft { get; set; }
    }


    public class UserOptions
    {
        public UserQuery Query { get; set; }
        public string UserName { get; set; }
        public bool? Include { get; set; }
        public bool? Like { get; set; }
    }

    public class SortOptions
    {
        public string Name { get; set; }
        public string Order { get; set; }
        public string Label { get; set; }
    }
}
