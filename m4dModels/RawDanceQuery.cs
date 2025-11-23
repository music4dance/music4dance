using DanceLibrary;

using System.Text.RegularExpressions;

namespace m4dModels
{
    /// <summary>
    /// Parses dance information from raw OData filter queries
    /// </summary>
    public partial class RawDanceQuery
    {
        private readonly string _odata;
        private readonly string _flags;

        public RawDanceQuery(string odata = null, string flags = null)
        {
            _odata = odata;
            _flags = flags;
        }

        /// <summary>
        /// Returns the query string (OData filter)
        /// </summary>
        public string Query => _odata ?? string.Empty;

        /// <summary>
        /// Returns true since raw queries always include all dances unless filtered
        /// </summary>
        public bool All => string.IsNullOrEmpty(_odata);

        /// <summary>
        /// Raw queries are always considered complex
        /// </summary>
        public bool IsComplex => true;

        /// <summary>
        /// Extracts dance items from the raw OData query
        /// </summary>
        public IEnumerable<DanceQueryItem> Items => DanceQueryItems;

        /// <summary>
        /// Extracts dance items from the raw OData query
        /// </summary>
        public IEnumerable<DanceQueryItem> DanceQueryItems
        {
            get
            {
                var dances = DanceObjects;
                return dances.Select(d => new DanceQueryItem
                {
                    Id = d.Id,
                    Threshold = 1
                });
            }
        }

        /// <summary>
        /// Gets the dance IDs from the query items
        /// </summary>
        public IEnumerable<string> DanceIds => Items.Select(d => d.Id);

        /// <summary>
        /// Gets the dance objects from the query items
        /// </summary>
        public IEnumerable<DanceObject> Dances => Items.Select(d => d.Dance);

        /// <summary>
        /// Raw queries don't support exclusive mode
        /// </summary>
        public bool IsExclusive => false;

        /// <summary>
        /// Returns true if this query represents a single dance
        /// </summary>
        public bool SingleDance => FlagList.Any(f =>
            string.Equals(f, "singledance", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets the short description of the dances
        /// </summary>
        public string ShortDescription => string.Join(", ", Items.Select(dt => dt.ShortDescription));

        /// <summary>
        /// Extracts dance objects from the OData filter
        /// </summary>
        private IEnumerable<DanceObject> DanceObjects
        {
            get
            {
                var danceName = ParseDance();
                if (danceName != null)
                {
                    var dance = DanceLibrary.Dances.Instance.DanceFromName(danceName);
                    if (dance != null)
                    {
                        yield return dance;
                    }
                }
            }
        }

        /// <summary>
        /// Parses the dance name from the OData filter using regex
        /// Matches patterns like: DanceTags/any(...'dance-name'...) or DanceTags/all(...'dance-name'...)
        /// </summary>
        private string ParseDance()
        {
            if (string.IsNullOrEmpty(_odata))
            {
                return null;
            }

            var match = DanceTagsRegex().Match(_odata);
            return match.Success && match.Groups.Count == 3 ? match.Groups[2].Value : null;
        }

        /// <summary>
        /// Splits the flags string into individual flag values
        /// </summary>
        public IList<string> FlagList =>
            string.IsNullOrEmpty(_flags)
                ? []
                : _flags.Split('|').ToList();

        /// <summary>
        /// Returns the filter string for OData queries
        /// </summary>
        public string GetODataFilter(DanceMusicCoreService dms)
        {
            // For raw queries, return the OData filter as-is
            return _odata;
        }

        /// <summary>
        /// Returns the sort fields for OData queries
        /// </summary>
        public IList<string> ODataSort(string order)
        {
            // For raw queries, no specific sort based on dance
            return [];
        }

        /// <summary>
        /// Returns a description of the dance query
        /// </summary>
        public override string ToString()
        {
            var items = Items.ToList();
            if (items.Count == 0)
            {
                return "songs";
            }
            else if (items.Count == 1)
            {
                return $"{items[0].Description} songs";
            }
            else
            {
                return $"songs matching {string.Join(", ", items.Select(i => i.Description))}";
            }
        }

        /// <summary>
        /// Regex to extract dance name from OData DanceTags filter
        /// Pattern: DanceTags/(any|all)(...'dance-name'...)
        /// </summary>
        [GeneratedRegex(@"DanceTags\/(any|all)\([^']*'(.*?)'", RegexOptions.IgnoreCase)]
        private static partial Regex DanceTagsRegex();
    }
}
