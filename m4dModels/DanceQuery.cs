using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using DanceLibrary;

namespace m4dModels
{
    public class DanceQuery
    {
        private const string AllRef = "ALL"; // Special 'All dances' value
        private const string And = "AND"; // Exclusive + Explicit

        private const string AndX = "ADX"; // Exclusive + Inferred

        //private const string OneOf = ""; // Inclusive + Explicit
        private const string OneOfX = "OOX"; // Inclusive + Inferred

        private readonly string[] _modifiers = [And, AndX, OneOfX];

        public DanceQuery(string query = null)
        {
            Query = query ?? string.Empty;
            if (string.Equals(AllRef, Query, StringComparison.InvariantCultureIgnoreCase))
            {
                Query = string.Empty;
            }
        }

        public string Query { get; }

        public bool All => string.IsNullOrEmpty(Query);

        public IEnumerable<DanceThreshold> DancesThresholds
        {
            get
            {
                var items = Query
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (items.Count > 0 && _modifiers.Contains(items[0].ToUpper()))
                {
                    items.RemoveAt(0);
                }

                return items.Select(DanceThreshold.FromValue);
            }
        }

        public IEnumerable<string> DanceIds => DancesThresholds.Select(d => d.Id);
        public IEnumerable<DanceObject> Dances => DancesThresholds.Select(d => d.Dance);

        public bool IsExclusive => (StartsWith(And) || StartsWith(AndX)) &&
            Query.IndexOf(',', 4) != -1;

        public string ODataFilter
        {
            get
            {
                var dances = DanceLibrary.Dances.Instance.ExpandGroups(Dances).ToList();
                if (dances.Count == 0)
                {
                    return null;
                }

                var sb = new StringBuilder();
                var con = IsExclusive ? "and" : "or";

                foreach (var d in dances)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append($" {con} ");
                    }

                    var threshold = DancesThresholds.FirstOrDefault(dt => dt.Id == d.Id);
                    if (threshold == null)
                    {
                        var groups = (d as DanceType)?.Groups;
                        if (groups != null)
                        {
                            threshold = DancesThresholds.FirstOrDefault(dt => groups.Any(g => g.Id == dt.Id));
                        }
                    }

                    if (threshold != null)
                    {
                        sb.AppendFormat(
                            "(dance_{0} {1} {2})",
                            d.Id, threshold.Threshold > 0 ? "ge" : "le", Math.Abs(threshold.Threshold));
                    }
                    else
                    {
                        Trace.WriteLine($"Invalid DanceQuery = {Query}, Dance = {d.Id}");
                    }
                }

                return $"({sb})";
            }
        }

        public IList<string> ODataSort(string order)
        {
            var dances = DanceLibrary.Dances.Instance.ExpandGroups(Dances).ToList();
            if (dances.Count == 0)
            {
                return [$"dance_ALL {order}"];
            }

            return [$"dance_{dances[0].Id} {order}"];
        }

        public string ShortDescription => string.Join(", ", DancesThresholds.Select(dt => dt.ShortDescription));

        public bool HasDance(string id)
        {
            return DanceIds.Contains(id, StringComparer.OrdinalIgnoreCase);
        }

        public DanceQuery AddDance(string dance)
        {
            var q = Query;
            q = string.IsNullOrWhiteSpace(q) ? dance : q + "," + dance;
            return new DanceQuery(q);
        }

        public DanceQuery MakeInclusive()
        {
            return IsExclusive
                ? new DanceQuery(
                    (StartsWith(AndX) ? OneOfX + "," : string.Empty) +
                    RemoveQualifier())
                : this;
        }

        public DanceQuery MakeExclusive()
        {
            return IsExclusive && DanceIds.Count() > 1
                ? this
                : new DanceQuery(
                    And + "," +
                    (StartsWith(OneOfX) ? RemoveQualifier() : Query));
        }

        public override string ToString()
        {
            var thresholds = DancesThresholds.ToList();
            var count = thresholds.Count;
            var prefix = IsExclusive ? "all" : "any";
            var connector = IsExclusive ? "and" : "or";

            switch (count)
            {
                case 0:
                    return "songs";
                case 1:
                    return $"{thresholds[0]} songs";
                case 2:
                    return
                        $"songs danceable to {prefix} of {thresholds[0]} {connector} {thresholds[1]}";
                default:
                    var last = thresholds[count - 1];
                    thresholds.RemoveAt(count - 1);
                    return
                        $"songs danceable to {prefix} of {string.Join(", ", thresholds)} {connector} {last}";
            }
        }

        private bool StartsWith(string qualifier)
        {
            return Query.StartsWith(qualifier + ",", StringComparison.InvariantCultureIgnoreCase);
        }

        private string RemoveQualifier()
        {
            return Query[4..];
        }
    }
}
