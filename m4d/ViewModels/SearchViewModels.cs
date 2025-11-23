using AutoMapper;

using DanceLibrary;

namespace m4d.ViewModels;

public class TagProfile : Profile
{
    public TagProfile()
    {
        _ = CreateMap<TagCount, TagModel>()
            .ForMember(dest => dest.Value, opt => opt.MapFrom(src => src.TagValue))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.TagClass));
        _ = CreateMap<TagGroup, TagModel>();
    }
}

public class SearchModel
{
    public SongFilterSparse Filter { get; set; }
    public List<DanceObject> Dances { get; set; }
}

public class TagModel
{
    public string Value { get; set; }
    public string Category { get; set; }
    public int Count { get; set; }
}
