using System.Text;

namespace m4dModels
{
    // normalize to (+|-)UserName\|[l|h]
    public class UserQuery
    {
        public const string IdentityUser = "me";

        public UserQuery(string query = null)
        {
            Query = Normalize(query);
        }

        public UserQuery(string userName, bool include, char? modifier)
        {
            var qs = Normalize(userName);
            if (!string.IsNullOrEmpty(qs) && !string.IsNullOrWhiteSpace(qs) && qs != "null")
            {
                if (!include)
                {
                    qs = "-" + qs[1..];
                }

                if (modifier.HasValue)
                {
                    qs += modifier;
                }
            }

            Query = qs;
        }

        public UserQuery(string userName, bool include, bool? like = null)
            : this(userName, include, like.HasValue ? (like.Value ? 'l' : 'h') : 'a')
        {
        }

        public UserQuery(UserQuery query, string userName)
            : this(userName, query.IsInclude, query.Modifier)
        {
        }

        private bool? NullableLike => IsLike ? true : IsHate ? false : null;

        public string Query { get; }
        public bool IsNull => string.Equals(Query, "null", StringComparison.OrdinalIgnoreCase);
        public bool IsEmpty => string.IsNullOrWhiteSpace(Query) || IsNull;
        public bool IsInclude => !IsEmpty && Query[0] == '+';
        public bool IsExclude => !IsEmpty && Query[0] == '-';
        public bool HasOpinion => !IsEmpty && IsLike || IsHate || IsAny;
        public bool IsLike => Modifier == 'l';
        public bool IsHate => Modifier == 'h';
        public bool IsAny => Modifier == 'a' || IsVoted;
        public bool IsUpVoted => Modifier == 'd';
        public bool IsDownVoted => Modifier == 'x';
        public bool IsVoted => IsDownVoted || IsUpVoted;
        public string UserName => IsEmpty ? null : Query[1..Query.IndexOf('|')];

        public bool IsIdentity =>
            string.Equals(IdentityUser, UserName, StringComparison.OrdinalIgnoreCase);

        public bool IsDefault(string user) =>
            IsEmpty || ((IsIdentity || string.Equals(
                user, UserName, StringComparison.OrdinalIgnoreCase)) && IsHate);

        public bool IsAnonymous =>
            !string.IsNullOrEmpty(UserName) && UserName.Length == 36 && UserName.Contains('-');

        public string ActionDescription
        {
            get
            {
                if (IsNull)
                {
                    return "Don't filter on user activity";
                }

                var start = IsInclude ? "Include only" : "Exclude all";
                var i = IsIdentity ? "I" : UserName;
                var end = Modifier switch
                {
                    'l' => $"{i} marked LIKE",
                    'h' => $"{i} marked DON'T LIKE",
                    'd' => $"{i} voted FOR",
                    'x' => $"{i} voted AGAINST",
                    _ => $"{i} tagged",
                };
                return $"{start} songs {end}";
            }
        }

        public string ODataFilter
        {
            get
            {
                if (IsEmpty || IsIdentity)
                {
                    return null;
                }

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

                // For the include case, explicitly drop the false case
                return IsInclude
                    ? IsAny ? $"({a} or {b} or {c})"
                    : $"({a} or {b})"
                    : $"({a} and {b} and {c})";
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
            if (query[0] is not '+' and not '-')
            {
                query = "+" + query;
            }

            if (!query.Contains('|'))
            {
                query += '|';
            }

            return query;
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

        private char? Modifier
        {
            get
            {
                var length = Query.Length;
                return (length > 2 && Query[length - 2] == '|') ? char.ToLower(Query[length - 1]) : null;
            }
        }


        public string Description(bool trivial = false)
        {
            if (IsEmpty)
            {
                return string.Empty;
            }

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

            _ = Modifier switch
            {
                'l' => ret.Append(" liked"),
                'h' => ret.Append(" liked"),
                'd' => ret.Append(" voted for"),
                'x' => ret.Append(" voted against"),
                _ => ret.Append(" edited"),
            };
            _ = ret.Append(" by ");

            _ = ret.Append(UserName);

            return ret.ToString();
        }

        public override string ToString()
        {
            return Description();
        }
    }
}
