using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using System.ComponentModel.DataAnnotations;

namespace m4dModels
{
    public class RawSearch
    {
        public RawSearch()
        {
        }

        public RawSearch(SongFilter songFilter)
        {
            if (songFilter == null || songFilter.IsEmpty)
            {
                return;
            }

            if (!songFilter.IsRaw)
            {
                throw new ArgumentException(
                    @"Can't cast SongFilter to RawSearch - try using DanceMusicService.AzureParmsFromFilter",
                    nameof(songFilter));
            }

            SearchText = songFilter.SearchString;
            ODataFilter = songFilter.Dances;
            SortFields = songFilter.SortOrder;
            SearchFields = songFilter.User;
            IsLucene = songFilter.IsLucene;
            CruftFilter = songFilter.Level.HasValue
                ? (CruftFilter)songFilter.Level.Value
                : m4dModels.CruftFilter.NoCruft;
            Flags = songFilter.Tags;
            Page = songFilter.Page;
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
        public CruftFilter CruftFilter { get; set; }

        public string Flags { get; set; }

        [Display(Name = @"Flags")]
        public int? Page { get; set; }

        public SearchOptions GetAzureSearchParams(int? pageSize)
        {
            var order = string.IsNullOrEmpty(SortFields) ? null : SortFields.Split('|').ToList();
            var fields = string.IsNullOrEmpty(SearchFields)
                ? null
                : SearchFields.Split('|').ToList();
            var ret = new SearchOptions
            {
                QueryType = IsLucene ? SearchQueryType.Full : SearchQueryType.Simple,
                Filter = ODataFilter,
                IncludeTotalCount = true,
                Skip = pageSize == -1 ? 0 : ((Page ?? 1) - 1) * pageSize,
                Size = (pageSize == -1) ? null : pageSize ?? 25,
            };
            ret.SearchFields.AddRange(fields);
            ret.OrderBy.AddRange(order);
            return ret;
        }

        public override string ToString()
        {
            return
                $"Raw Azure Search: Search String = \"{SearchText}\", Filter=\"{ODataFilter}\" Sort Fields = \"{SortFields}\" Search Fields = \"{SearchFields}\" Description = \"{Description}\" Lucene = {IsLucene}";
        }
    }
}
