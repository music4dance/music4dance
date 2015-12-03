using System.Linq;

namespace m4dModels
{
    // normalize to (+|-)UserName\|[L|H]
    public class UserQuery
    {
        public UserQuery(string query = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Query = string.Empty;
            }
            else
            {
                Query = query.Trim().ToLower();
                if (Query[0] != '+' && Query[0] != '-')
                {
                    Query = "+" + Query;
                }
                if (!Query.Contains('|'))
                {
                    Query += '|';
                }
            }
        }

        public string Query { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Query);
        public bool IsInclude => !IsEmpty && Query[0] == '+';
        public bool IsExclude => !IsEmpty && Query[0] == '-';
        public bool HasOpinion => !IsEmpty && !Query.EndsWith("|");
        public bool IsLike => Query.EndsWith("|l");
        public bool IsHate => Query.EndsWith("|h");
        public string UserName => IsEmpty ? null : Query.Substring(1, Query.IndexOf('|') - 1);

    }
}
