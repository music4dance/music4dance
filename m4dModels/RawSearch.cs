using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Principal;
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
            if (songFilter.IsEmpty) return;

            if (!songFilter.IsRaw) throw new ArgumentException(@"Can't cast SongFilter to RawSearch - try using DanceMusicService.AzureParmsFromFilter", nameof(songFilter));

            SearchText = songFilter.SearchString;
            Filter = songFilter.Dances;
            Sort = songFilter.SortOrder;
            IsLucene = songFilter.IsLucene;

            Page = songFilter.Page;
        }

        public RawSearch(string val) : this(new SongFilter(val))
        {
        }

        [Display(Name = @"Search Text")]
        public string SearchText { get; set; }
        [Display(Name = @"OData Filter")]
        public string Filter { get; set; }
        [Display(Name = @"Sort")]
        public string Sort { get; set; }
        [Display(Name = @"Use Lucene Syntax")]
        public bool IsLucene { get; set; }

        public int? Page { get; set; }

        public SearchParameters GetAzureSearchParams(int? pageSize)
        {
            var order = string.IsNullOrEmpty(Sort) ? null : Sort.Split('|').ToList();

            return new SearchParameters
            {
                QueryType = IsLucene ? QueryType.Full : QueryType.Simple,
                Filter = Filter,
                IncludeTotalResultCount = true,
                Top = pageSize??25,
                OrderBy = order
            };
        }

        public override string ToString()
        {
            return $"Raw Azure Search: Search String = \"{SearchText}\", Filter=\"{Filter}\" Sort = \"{Sort}\" Lucene = {IsLucene}";
        }
    }
}
