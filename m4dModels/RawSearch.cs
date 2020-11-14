using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Microsoft.Azure.Search.Models;

namespace m4dModels
{
    public class RawSearch
    {
        public RawSearch()
        {
        }
        public RawSearch(SongFilter songFilter)
        {
            if (songFilter == null || songFilter.IsEmpty) return;

            if (!songFilter.IsRaw) throw new ArgumentException(@"Can't cast SongFilter to RawSearch - try using DanceMusicService.AzureParmsFromFilter", nameof(songFilter));

            SearchText = songFilter.SearchString;
            ODataFilter = songFilter.Dances;
            SortFields = songFilter.SortOrder;
            SearchFields = songFilter.User;
            IsLucene = songFilter.IsLucene;
            CruftFilter = songFilter.Level.HasValue ? 
                (DanceMusicService.CruftFilter) songFilter.Level.Value : 
                DanceMusicService.CruftFilter.NoCruft;
            Flags = songFilter.Tags;
            Page = songFilter.Page;
        }

        public RawSearch(string val) : this(new SongFilter(val))
        {
        }

        [Display(Name = @"Search Text")]
        public string SearchText { get; set; }
        [Display(Name = @"OData Filter")]
        public string ODataFilter { get; set; }
        [Display(Name = @"Sort Fields")]
        public string SortFields { get; set; }
        [Display(Name = @"Search Fields")]
        public string SearchFields { get; set; }
        [Display(Name = @"Description")]
        public string Description { get; set; }
        [Display(Name = @"Use Lucene Syntax")]
        public bool IsLucene { get; set; }
        [Display(Name = @"CruftFilter")]
        public DanceMusicService.CruftFilter CruftFilter { get; set; }
        public string Flags { get; set; }
        [Display(Name = @"Flags")]

        public int? Page { get; set; }

        public SearchParameters GetAzureSearchParams(int? pageSize)
        {
            var order = string.IsNullOrEmpty(SortFields) ? null : SortFields.Split('|').ToList();
            var fields = string.IsNullOrEmpty(SearchFields) ? null : SearchFields.Split('|').ToList();
            return new SearchParameters
            {
                QueryType = IsLucene ? QueryType.Full : QueryType.Simple,
                SearchFields = fields,
                Filter = ODataFilter,
                IncludeTotalResultCount = true,
                Skip = ((Page ?? 1) - 1) * pageSize,
                Top = pageSize??25,
                OrderBy = order
            };
        }

        public string QueryString()
        {
            var sb = new StringBuilder("");

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                sb.Append($"SearchText={SearchText}&");
            }
            if (!string.IsNullOrWhiteSpace(ODataFilter))
            {
                sb.Append($"ODataFilter={ODataFilter}&");
            }
            if (!string.IsNullOrWhiteSpace(SearchFields))
            {
                sb.Append($"SearchFields={SearchFields}&");
            }
            if (!string.IsNullOrWhiteSpace(Description))
            {
                sb.Append($"Description={Description}&");
            }
            if (IsLucene)
            {
                sb.Append("IsLucene=true&");
            }
            if (CruftFilter != DanceMusicService.CruftFilter.NoCruft)
            {
                sb.Append($"CruftFilter={CruftFilter}&");
            }

            return "?" + sb;
        }

        public override string ToString()
        {
            return $"Raw Azure Search: Search String = \"{SearchText}\", Filter=\"{ODataFilter}\" Sort Fields = \"{SortFields}\" Search Fields = \"{SearchFields}\" Description = \"{Description}\" Lucene = {IsLucene}";
        }
    }
}
