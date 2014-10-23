using System.ComponentModel;
namespace m4dModels
{
    [TypeConverter(typeof(SongFilterConverter))]
    public class SongFilter
    {
        private const string empty = ".";

        static public SongFilter Default
        {
            get
            {
                return _default;
            }
        }
        static private SongFilter _default = new SongFilter();

        public SongFilter()
        {
            Action = "Index";
        }

        public SongFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            bool fancy = false;
            if (value.Contains("\\-"))
            {
                fancy = true;
                value = value.Replace("\\-", "~");
            }

            string[] cells = value.Split(new char[] { '-' });

            for (int i = 0; i < cells.Length; i++)
            {
                if (string.Equals(cells[i], empty))
                {
                    cells[i] = string.Empty;
                }
                
                if (fancy)
                {
                    cells[i] = cells[i].Replace('~', '-');
                }
            }

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
                User = cells[5];
            }
            if (cells.Length > 6 && !string.IsNullOrWhiteSpace(cells[6]))
            {
                decimal minTempo = 0;
                if (decimal.TryParse(cells[6], out minTempo))
                {
                    TempoMin = minTempo;
                }
            }
            if (cells.Length > 7 && !string.IsNullOrWhiteSpace(cells[7]))
            {
                decimal maxTempo = 0;
                if (decimal.TryParse(cells[7], out maxTempo))
                {
                    TempoMax = maxTempo;
                }
            }
            if (cells.Length > 8 && !string.IsNullOrWhiteSpace(cells[8]))
            {
                int page = 0;
                if (int.TryParse(cells[8], out page))
                {
                    Page = page;
                }
            }
            if (cells.Length > 9 && !string.IsNullOrWhiteSpace(cells[9]))
            {
                int level = 0;
                if (int.TryParse(cells[9], out level))
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
        public string User { get; set; }
        public decimal? TempoMin { get; set; }
        public decimal? TempoMax { get; set; }
        public int? Page { get; set; }
        public int? Level { get; set; }

        public override string ToString()
        {
            string ret = string.Format("{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}-{8}-{9}",
                Format(Action),
                Format(Dances),
                Format(SortOrder),
                Format(SearchString),
                Format(Purchase),
                Format(User),
                TempoMin.HasValue ? Format(TempoMin.Value.ToString()) : empty,
                TempoMax.HasValue ? Format(TempoMax.Value.ToString()) : empty,
                Page.HasValue ? Format(Page.Value.ToString()) : empty,
                Level.HasValue ? Format(Level.Value.ToString()) : empty
                );

            return ret;
        }

        private string Format(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return empty;
            }
            else if (s.Contains("-"))
            {
                return s.Replace("-", "\\-");
            }
            else
            {
                return s;
            }
        }
    }
}