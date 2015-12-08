using System;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace m4dModels
{
    // normalize to (+|-)UserName\|[L|H]
    public class UserQuery
    {
        public UserQuery(string query = null)
        {
            Query = Normalize(query);
        }

        public UserQuery(UserQuery query, string userName)
        {
            var qs = Normalize(userName);
            if (!query.IsEmpty && !string.IsNullOrWhiteSpace(qs) && qs != "null")
            {
                if (query.IsExclude)
                {
                    qs = "-" + qs.Substring(1);
                }
                if (query.IsLike)
                {
                    qs += 'L';
                }
                else if (query.IsHate)
                {
                    qs += 'H';
                }
            }
            Query = qs;
        }

        private static string Normalize(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return string.Empty;
            }
            if (string.Equals(query, "null", StringComparison.OrdinalIgnoreCase))
            {
                return "null";
            }

            query = query.Trim().ToLower();
            if (query[0] != '+' && query[0] != '-')
            {
                query = "+" + query;
            }
            if (!query.Contains('|'))
            {
                query += '|';
            }
            return query;
        }

        public string Query { get; }
        public bool IsNull => string.Equals(Query, "null", StringComparison.OrdinalIgnoreCase);
        public bool IsEmpty => string.IsNullOrWhiteSpace(Query) || IsNull;
        public bool IsInclude => !IsEmpty && Query[0] == '+';
        public bool IsExclude => !IsEmpty && Query[0] == '-';
        public bool HasOpinion => !IsEmpty && !Query.EndsWith("|");
        public bool IsLike => Query.EndsWith("|l");
        public bool IsHate => Query.EndsWith("|h");
        public string UserName => IsEmpty ? null : Query.Substring(1, Query.IndexOf('|') - 1);

        public string Description(bool trivial = false)
        {
            if (IsEmpty) return string.Empty;

            string start;
            if (trivial)
            {
                start = IsInclude ? string.Empty : " not";
            }
            else
            {
                start = IsInclude ? " including songs" : " excluding songs";
            }
            var ret = new StringBuilder(start);

            if (IsLike)
            {
                ret.Append(" liked by ");
            }
            else if (IsHate)
            {
                ret.Append(" disliked by ");
            }
            else
            {
                ret.Append(" edited by ");
            }

            ret.Append(UserName);

            return ret.ToString();
        }

        public override string ToString()
        {
            return Description();
        }
    }
}
