using System;
using System.Collections.Generic;
using AutoMapper;

namespace m4dModels
{
    public class SongPropertyProfile : Profile
    {
        public SongPropertyProfile()
        {
            CreateMap<SongProperty, SongPropertySparse>();
            CreateMap<SongPropertySparse, SongProperty>();
        }
    }

    public class SongPropertySparse
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class SongHistory
    {
        public Guid Id { get; set; }
        public List<SongPropertySparse> Properties { get; set; }
    }
}