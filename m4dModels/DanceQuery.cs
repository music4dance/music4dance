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
        private const string AllRef = "ALL";
        private const string And = "AND"; // Exclusive + Explicit
        private const string AndX = "ADX"; // Exclusive + Inferred
        //private const string OneOf = ""; // Inclusive + Explicit
        private const string OneOfX = "OOX"; // Inclusive + Inferred

        private readonly string[] _modifiers = {And,AndX,OneOfX};

        public DanceQuery(string query=null)
        {
            Query = query ?? string.Empty;
            if (string.Equals(AllRef, Query, StringComparison.InvariantCultureIgnoreCase))
            {
                Query = string.Empty;
            }
        }

        public string Query { get; }

        public bool All => string.IsNullOrEmpty(Query);

        public IEnumerable<string> DanceIds
        {
            get
            {
                var ret = Query.Split(new[] {','},StringSplitOptions.RemoveEmptyEntries);
                if (ret.Length == 0 || !_modifiers.Contains(ret[0].ToUpper()))
                    return ret;

                var list = ret.ToList();
                list.RemoveAt(0);
                ret = list.ToArray();
                return ret;
            }
        }
        public IEnumerable<DanceObject> Dances => DanceLibrary.Dances.Instance.FromIds(DanceIds);

        public bool Advanced => DanceIds.Count() > 1;

        public IEnumerable<string> ExpandedIds => DanceLibrary.Dances.Instance.ExpandMsc(DanceIds);

        public bool IsExclusive => (StartsWith(And) || StartsWith(AndX)) && Query.IndexOf(",", 4, StringComparison.Ordinal) != -1;
        public bool IncludeInferred => StartsWith(AndX) || StartsWith(OneOfX);

        public bool HasDance(string id)
        {
            return DanceIds.Contains(id, StringComparer.OrdinalIgnoreCase);
        }

        //public bool IsDanceOrEmpty(string id)
        //{
        //    var ids = DanceIds as string[];
        //    Debug.Assert(ids != null);
        //    return ids.Length == 0 || (ids.Length == 1 && string.Equals(id, ids[0]));
        //}

        public DanceQuery AddDance(string dance)
        {
            var q = Query;
            q = string.IsNullOrWhiteSpace(q) ? dance : q + "," + dance;
            return new DanceQuery(q);
        }

        public DanceQuery MakeInclusive()
        {
            return IsExclusive ? new DanceQuery((StartsWith(AndX) ? (OneOfX + ",") : string.Empty) + RemoveQualifier()) : this;
        }

        public DanceQuery MakeExclusive()
        {
            return (IsExclusive && DanceIds.Count() > 1) ? 
                this : new DanceQuery((IncludeInferred ? AndX : And) + "," + (StartsWith(OneOfX) ? RemoveQualifier() : Query));
        }

        public DanceQuery MakeInferred()
        {
            return IncludeInferred ? this : new DanceQuery(IsExclusive ? AndX + "," + RemoveQualifier() : OneOfX + "," + Query);
        }

        public DanceQuery MakeExplicit()
        {
            return !IncludeInferred ? this : new DanceQuery((IsExclusive ? And + "," : string.Empty) + "," + RemoveQualifier());
        }

        public string ODataFilter
        {
            get
            {
                var dances = Dances.ToList();
                switch (dances.Count)
                {
                    case 0:
                        return null;
                    case 1:
                        return $"(DanceTags/any(t: t eq '{dances[0].Name.ToLower()}')" + 
                            (IncludeInferred ? $" or  DanceTagsInferred/any(t: t eq '{dances[0].Name.ToLower()}')" : "") + ")";
                }

                var sb = new StringBuilder();
                var con = IsExclusive ? "and" : "or";

                foreach (var d in dances)
                {
                    if (sb.Length > 0) sb.Append($" {con} ");
                    sb.AppendFormat("(DanceTags/any(t: t eq '{0}')", d.Name.ToLower());
                    if (IncludeInferred)
                    {
                        sb.AppendFormat(" or DanceTagsInferred/any(t: t eq '{0}')", d.Name.ToLower());
                    }
                    sb.Append(")");
                }

                return $"({sb})";
            }
        }

        public override string ToString()
        {
            var dances = Dances.Select(n => n.Name).ToList();
            var count = dances.Count;
            var prefix = "any";
            var connector = "or";
            if (IsExclusive)
            {
                prefix = "all";
                connector = "and";
            }
            var suffix = string.Empty;
            if (IncludeInferred)
            {
                suffix = " (including inferred by tempo)";
            }
            switch (count)
            {
                case 0:
                    return "songs" + suffix;
                case 1:
                    return $"{dances[0]} songs{suffix}";
                case 2:
                    return $"songs dancable to {prefix} of {dances[0]} {connector} {dances[1]}{suffix}";
                default:
                    var last = dances[count - 1];
                    dances.RemoveAt(count - 1);
                    return $"songs danceable to {prefix} of {string.Join(", ", dances)} {connector} {last}{suffix}";
            }
        }

        public string ShortDescription => string.Join(", ", Dances.Select(n => n.Name));

        private bool StartsWith(string qualifier)
        {
            return Query.StartsWith(qualifier + ",", StringComparison.InvariantCultureIgnoreCase);
        }

        private string RemoveQualifier()
        {
            return Query.Substring(4);
        }
    }
}
