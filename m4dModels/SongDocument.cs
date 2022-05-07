using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;

namespace m4dModels
{
    public class SongDocument
    {
        // Song History Equivalent
        [JsonPropertyName("SongId")]
        [SimpleField(IsKey = true)]
        public string Id { get; set; }

        [SimpleField]
        public string Properties { get; set; }

        // Internal management
        [SimpleField(IsFilterable = true)]
        public List<string> AlternateIds { get; set; }

        [SimpleField(IsFilterable = true)]
        public int TitleHash { get; set; }

        [SimpleField(IsFilterable = true)]
        public List<string> ServiceIds { get; set; }

        // Basic Song Info

        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Title { get; set; }

        [SearchableField(IsFilterable = true, IsSortable = true)]
        public string Artist { get; set; }

        [SearchableField(IsFilterable = true)]
        public List<string> Album { get; set; }

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public double Tempo { get; set; }

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public int Length { get; set; }

        // Dates

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public DateTimeOffset Created { get; set; }

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public DateTimeOffset Modified { get; set; }

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public DateTimeOffset Edited { get; set; }


        [SearchableField(IsFilterable = true)]
        public List<string> Users { get; set; }


        [SimpleField(IsSortable = true, IsFilterable = true)]
        public double Beat { get; set; }

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public double Energy { get; set; }

        [SimpleField(IsSortable = true, IsFilterable = true)]
        public double Mood { get; set; }

        [SimpleField(IsFilterable = true)]
        public List<string> Purchase { get; set; }

        // Song Tags

        [SearchableField(IsFilterable = true, IsFacetable = true, IsSortable = true)]
        public List<string> DanceTags { get; set; }

        [SearchableField(IsFilterable = true, IsFacetable = true, IsSortable = true)]
        public List<string> GenreTags { get; set; }

        [SearchableField(IsFilterable = true, IsFacetable = true, IsSortable = true)]
        public List<string> StyleTags { get; set; }

        [SearchableField(IsFilterable = true, IsFacetable = true, IsSortable = true)]
        public List<string> TempoTags { get; set; }

        [SearchableField(IsFilterable = true, IsFacetable = true, IsSortable = true)]
        public List<string> OtherTags { get; set; }

        [SimpleField(IsFilterable = true)]
        public string Sample { get; set; }

        [SearchableField(IsFilterable = true)]
        public string Comment { get; set; }
    }
}
