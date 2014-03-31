using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace m4d.ViewModels
{
    public class SongFilter
    {
        public SongFilter()
        {

        }

        public SongFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string[] cells = value.Split(new char[] { '|' });
            if (cells.Length > 0 && !string.IsNullOrWhiteSpace(cells[0]))
            {
                Action = cells[0];
            }
            if (cells.Length > 1 && !string.IsNullOrWhiteSpace(cells[1]))
            {
                Dances = cells[1];
            }
            if (cells.Length > 2 && !string.IsNullOrWhiteSpace(cells[2]))
            {
                SortOrder = cells[2];
            }
            if (cells.Length > 3 && !string.IsNullOrWhiteSpace(cells[3]))
            {
                SearchString = cells[3];
            }
            if (cells.Length > 4 && !string.IsNullOrWhiteSpace(cells[4]))
            {
                Purchase = cells[4];
            }
            if (cells.Length > 5 && !string.IsNullOrWhiteSpace(cells[5]))
            {
                int page = 0;
                if (int.TryParse(cells[5], out page))
                {
                    Page = page;
                }
            }
            if (cells.Length > 6 && !string.IsNullOrWhiteSpace(cells[6]))
            {
                int level = 0;
                if (int.TryParse(cells[6], out level))
                {
                    Level = level;
                }
            }
        }
        public string Action { get; set; }
        public string Dances { get; set; }
        public string SortOrder { get; set; }
        public string SearchString { get; set; }
        public string Purchase { get; set; }
        public int? Page { get; set; }
        public int? Level { get; set; }

        public override string ToString()
        {
            string ret = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                Action ?? string.Empty,
                Dances ?? string.Empty,
                SortOrder ?? string.Empty,
                SearchString ?? string.Empty,
                Purchase ?? string.Empty,
                Page.HasValue ? Page.Value.ToString() : string.Empty,
                Level.HasValue ? Level.Value.ToString() : string.Empty
                );

            return ret;
        }
    }
}