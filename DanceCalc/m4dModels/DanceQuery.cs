using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DanceLibrary;

namespace m4dModels
{
    public class DanceQuery
    {
        public DanceQuery(string query=null)
        {
            Query = query ?? string.Empty;
            if (string.Equals("ALL", Query, StringComparison.InvariantCultureIgnoreCase))
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
                if (ret.Length == 0 || !string.Equals("AND", ret[0], StringComparison.InvariantCultureIgnoreCase))
                    return ret;

                var list = ret.ToList();
                list.RemoveAt(0);
                ret = list.ToArray();
                return ret;
            }
        }
        public IEnumerable<DanceObject> Dances
        {
            get
            {
                var ret = new List<DanceObject>();

                foreach (var id in DanceIds)
                {
                    DanceObject o;
                    if (DanceLibrary.Dances.Instance.DanceDictionary.TryGetValue(id, out o))
                    {
                        ret.Add(o);
                    }
                }

                return ret;
            }
        }

        public bool Advanced => DanceIds.Count() > 1;

        public IEnumerable<string> ExpandedIds => DanceLibrary.Dances.Instance.ExpandMsc(DanceIds);

        public bool IsExclusive => Query.StartsWith("AND,",StringComparison.InvariantCultureIgnoreCase) && Query.IndexOf(",",4, StringComparison.Ordinal) != -1;

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
            return IsExclusive ? new DanceQuery(Query.Substring(4)) : this;
        }

        public DanceQuery MakeExclusive()
        {
            return IsExclusive && DanceIds.Count() > 1 ? this : new DanceQuery("AND," + Query);
        }
    }
}
