using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using DanceLibrary;

namespace m4dModels
{
    public class DanceQuery2(string query = null) : DanceQuery(query)
    {
        public override string ODataFilter
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
                            "(dance_{0}/Votes {1} {2})",
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

        public override IList<string> ODataSort(string order)
        {
            var dances = DanceLibrary.Dances.Instance.ExpandGroups(Dances).ToList();
            if (dances.Count == 0)
            {
                return [$"dance_ALL/Votes {order}"];
            }

            return [$"dance_{dances[0].Id}/Votes {order}"];
        }
    }
}
