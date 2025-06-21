using System.Collections.Generic;

using FacetResults =
    System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<
        Azure.Search.Documents.Models.FacetResult>>;

namespace m4dModels
{
    public class SearchResults
    {
        public SearchResults(string query, int count, long totalCount, int currentPage,
            int pageSize, IEnumerable<Song> songs, FacetResults facets)
        {
            Query = query;
            Count = count;
            RawCount = TotalCount = totalCount;
            CurrentPage = currentPage;
            PageSize = pageSize;
            Songs = songs;
            FacetResults = facets;
        }

        public SearchResults(SearchResults result, List<Song> songs, int? totalCount = null)
        {
            Query = result.Query;
            TotalCount = totalCount ?? songs.Count;
            Count = songs.Count;
            RawCount = result.TotalCount;
            CurrentPage = result.CurrentPage;
            PageSize = result.PageSize;
            Songs = songs;
            FacetResults = result.FacetResults;
        }

        public string Query { get; }
        public int Count { get; }
        public long TotalCount { get; }
        public long RawCount { get; }
        public int CurrentPage { get; }
        public int PageSize { get; }
        public IEnumerable<Song> Songs { get; }

        public FacetResults FacetResults { get; }
    }
}
