using System;

using Azure.Search.Documents.Indexes;

namespace m4dModels
{
    public class PageSearch
    {
        [SimpleField(IsKey = true)]
        public string Url { get; set; }
        [SearchableField]
        public string Title { get; set; }
        [SearchableField]
        public string Description { get; set; }
        [SearchableField]
        public string Content { get; set; }

        public PageSearch GetEncoded() => Recode(
            s => s.Replace("/", "_")
            .Replace("(", "=OP=")
            .Replace(")", "=CP=")
            .Replace("?", "=QST=")
        );

        public PageSearch GetDecoded() => Recode(
            s => "/" + s
            .Replace("_", "/")
            .Replace("=OP=", "(")
            .Replace("=CP=", ")")
            .Replace("=QST=", "?")
        );

        private PageSearch Recode(Func<string, string> replace)
        {
            var coded = MemberwiseClone() as PageSearch;
            coded.Url = replace(Url);
            return coded;
        }
    }
}
