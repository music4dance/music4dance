using System.Collections.Generic;
using Microsoft.Azure.Search.Models;

namespace m4dModels
{
    public class SearchResults
    {
        public SearchResults(string query, int count, long totalCount, int currentPage, int pageSize, IEnumerable<SongBase> songs, FacetResults facets)
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
        public IEnumerable<SongBase> Songs { get; }

        public FacetResults FacetResults { get; }
    }
}
