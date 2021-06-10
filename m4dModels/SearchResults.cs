using System.Collections.Generic;
using FacetResults =
    System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<
        Microsoft.Azure.Search.Models.FacetResult>>;

namespace m4dModels
{
    public class SearchResults
    {
        public SearchResults(string query, int count, long totalCount, int currentPage,
            int pageSize, IEnumerable<Song> songs, FacetResults facets)
        {
            Query = query;
            Count = count;
            TotalCount = totalCount;
            CurrentPage = currentPage;
            PageSize = pageSize;
            Songs = songs;
            FacetResults = facets;
        }

        public string Query { get; }
        public int Count { get; }
        public long TotalCount { get; }
        public int CurrentPage { get; }
        public int PageSize { get; }
        public IEnumerable<Song> Songs { get; }

        public FacetResults FacetResults { get; }
    }
}
