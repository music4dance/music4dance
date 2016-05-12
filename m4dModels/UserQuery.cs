using System;
using System.Linq;
using System.Text;

namespace m4dModels
{
    // normalize to (+|-)UserName\|[l|h]
    public class UserQuery
    {
        public UserQuery(string query = null)
        {
            Query = Normalize(query);
        }


        public UserQuery(string userName, bool include, bool? like)
        {
            var qs = Normalize(userName);
            if (!string.IsNullOrEmpty(qs) && !string.IsNullOrWhiteSpace(qs) && qs != "null")
            {
                if (!include)
                {
                    qs = "-" + qs.Substring(1);
                }
                if (like.HasValue)
                {
                    if (like.Value)
                    {
                        qs += 'l';
                    }
                    else
                    {
                        qs += 'h';
                    }
                }
            }
            Query = qs;
        }

        public UserQuery(UserQuery query, string userName) : this(userName, query.IsInclude, query.NullableLike)
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
                    qs += 'l';
                }
                else if (query.IsHate)
                {
                    qs += 'h';
                }
            }
            Query = qs;
        }

        private bool? NullableLike
        {
            get
            {
                if (IsLike) return true;
                if (IsHate) return false;
                return null;
            }
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
        public bool IsLike => Query.EndsWith("|l") || Query.EndsWith("|L");
        public bool IsHate => Query.EndsWith("|h") || Query.EndsWith("|H");
        public string UserName => IsEmpty ? null : Query.Substring(1, Query.IndexOf('|') - 1);
        public bool IsAnonymous => string.Equals(AnonymousUser, UserName,StringComparison.OrdinalIgnoreCase);

        public const string AnonymousUser = "me";

        public string ActionDescription
        {
            get
            {
                if (IsNull) return "Don't filter on my activity";
                var start = IsInclude ? "Include only" : "Exclude all";
                string end;
                if (IsLike)
                {
                    end = "I marked LIKE";
                }
                else if (IsHate)
                {
                    end = "I marked DON'T LIKE";
                }
                else
                {
                    end = "I tagged";
                }

                return $"{start} songs {end}";
            }
        }

        public string ODataFilter
        {
            get
            {
                if (IsEmpty || IsAnonymous) return null;

                var inc = "any";
                var cmp = "eq";
                if (IsExclude)
                {
                    inc = "all";
                    cmp = "ne";
                }

                var like = NullableLike;
                var userName = UserName.ToLower();

                if (like.HasValue)
                {
                    return MakeOneOdata(userName, inc, cmp, NullableLike);
                }

                var a = MakeOneOdata(userName, inc, cmp, null);
                var b = MakeOneOdata(userName, inc, cmp, true);
                var c = MakeOneOdata(userName, inc, cmp, false);

                return IsInclude ? $"({a} or {b} or {c})" : $"!({a} and {b} and {c})";
            }
        }

        private static string MakeOneOdata(string userName, string inc, string cmp, bool? like)
        {
            var vote = string.Empty;
            if (like.HasValue)
            {
                vote = like.Value ? "|l" : "|h";
            }

            return $"Users/{inc}(t: t {cmp} '{userName}{vote}')";
        }

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
                ret.Append(" liked");
            }
            else if (IsHate)
            {
                ret.Append(" disliked");
            }
            else
            {
                ret.Append(" edited");
            }
            ret.Append(" by ");

            ret.Append(UserName);

            return ret.ToString();
        }

        public override string ToString()
        {
            return Description();
        }
    }
}
