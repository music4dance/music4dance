using System;
using System.Collections.Generic;

namespace SongDatabase.Models
{
    public class Song
    {
        public int SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public int Track { get; set; }
        public int Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public int TitleHash { get; set; }
        public virtual ICollection<Dance> Dances { get; set; }
        public virtual ICollection<UserProfile> ModifiedBy { get; set; }
        public virtual ICollection<SongProperty> SongProperties { get; set; }
    }

    public class SongProperty
    {
        public Int64 Id { get; set; }
        public int SongId { get; set; }
        public virtual Song Song { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}