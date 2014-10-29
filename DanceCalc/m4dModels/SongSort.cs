using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class SongSort
    {
        private static string[] directional = new string[] { "Title", "Artist", "Album", "Tempo" };
        private static string[] numerical = new string[] { "Tempo", "Modified", "Created" };

        private static string sortAsc = "<span class='glyphicon glyphicon-sort-by-alphabet'></span>";
        private static string sortDsc = "<span class='glyphicon glyphicon-sort-by-alphabet-alt'></span>";
        private static string sortNAsc = "<span class='glyphicon glyphicon-sort-by-order'></span>";
        private static string sortNDsc = "<span class='glyphicon glyphicon-sort-by-order-alt'></span>";
        public SongSort(string sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                sort = "Title";
            }
            List<string> list = sort.Split(new char[] { '_' }, StringSplitOptions.None).ToList();
            int count = -1;

            Id = list[0];
            list.RemoveAt(0);

            if (list.Count > 0)
            {
                if (string.Equals(list[0],"desc",StringComparison.OrdinalIgnoreCase))
                {
                    Descending = true;
                    list.RemoveAt(0);
                }
                else if (string.Equals(list[0], "asc", StringComparison.OrdinalIgnoreCase))
                {
                    list.RemoveAt(0);
                }
            }

            if (list.Count > 0)
            {
                if (!int.TryParse(list[0], out count))
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceError, string.Format("Bad Sort: {0}", sort));
                }
            }
            Count = count;
        }

        public string Id { get; private set; }
        public bool Descending { get; private set; }
        public int Count { get; private set; }

        public bool Numeric { get { return numerical.Contains(Id); } }
        public bool Directional { get { return directional.Contains(Id); } }
        public string GetSortGlyph(string column) 
        {
            string ret = string.Empty;
            if (column == Id)
            {
                if (numerical.Contains(Id))
                {
                    ret = Descending ? sortNDsc : sortNAsc;
                }
                else
                {
                    ret = Descending ? sortDsc : sortAsc;
                }
            }
            return ret;
        }
        public void Resort(string newOrder)
        {
            if (newOrder == Id && directional.Contains(newOrder))
            {
                Descending = !Descending;
            }
            else
            {
                Id = newOrder;
                Descending = false;
            }
        }
        public override string ToString()
        {
            return string.Format("{0}{1}", Id, Descending ? "_desc" : string.Empty);
        }

        public static string DoSort(string newOrder, string oldOrder)
        {
            SongSort ss;
            if (!string.IsNullOrWhiteSpace(oldOrder))
            {
                ss = new SongSort(oldOrder);
                ss.Resort(newOrder);
            }
            else
            {
                ss = new SongSort(newOrder);
            }
            return ss.ToString();
        }
    }
}
