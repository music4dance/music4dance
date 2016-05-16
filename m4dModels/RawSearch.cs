using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;

namespace m4dModels
{
    public class RawSearch
    {
        public RawSearch()
        {
        }
        public RawSearch(SongFilter filter)
        {
            
        }

        public RawSearch(string val)
        {
            var sf = new SongFilter(val);
            if (!sf.IsRaw) throw new ArgumentException(@"Can't cast SongFilter to RawSearch - try using DanceMusicService.AzureParmsFromFilter",nameof(sf));

            Text = sf.SearchString;
            Filter = sf.Dances;
            Sort = sf.SortOrder;
            IsLucene = sf.IsLucene;
        }

        public string Text { get; set; }
        public string Filter { get; set; }
        public string Sort { get; set; }
        public bool IsLucene { get; set; }

        public SearchParameters AzureSearchParams 
        {
            get
            {
                var order = string.IsNullOrEmpty(Sort) ? null : Sort.Split('|').ToList();

                return new SearchParameters
                {
                    QueryType = IsLucene ? QueryType.Full : QueryType.Simple,
                    Filter = Filter,
                    Top = 25,
                    OrderBy = order
                };
            }
        }

        public override string ToString()
        {
            return $"Raw Azure Search: Search String = \"{Text}\", Filter=\"{Filter}\" Sort = \"{Sort}\" Lucene = {IsLucene}";
        }
    }
}
