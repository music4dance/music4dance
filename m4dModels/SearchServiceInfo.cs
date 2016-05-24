using System;
using System.Collections.Generic;

namespace m4dModels
{
    public class SearchServiceInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string Index { get; }
        public string AdminKey { get; }
        public string QueryKey { get; }

        private SearchServiceInfo(string id, string name, string index, string adminKey, string queryKey)
        {
            Id = id;
            Name = name;
            Index = index;
            AdminKey = adminKey;
            QueryKey = queryKey;
        }

        public static SearchServiceInfo GetInfo(string id)
        {
            if (id == "default")
            {
                id = DefaultId;
            }

            return s_info[id];
        }

        private const string FreeAdmin = "***REMOVED***";
        private const string FreeQuery = "5B2BAFC30F0CD25405A10B08582B5451";

        private const string BasicAdmin = "***REMOVED***";
        private const string BasicQuery = "***REMOVED***";

        public static string DefaultId
        {
            get
            {
                return s_defaultId = s_defaultId ?? Environment.GetEnvironmentVariable("SEARCHINDEX") ?? "basica";
            }
            set
            {
                s_defaultId = value;
            }
        }
        private static string s_defaultId;

        private static readonly Dictionary<string, SearchServiceInfo> s_info = new Dictionary<string, SearchServiceInfo>
        {
            {
                "free",
                new SearchServiceInfo("default", "m4d", "songs", FreeAdmin, FreeQuery)
            },
            {
                "basica",
                new SearchServiceInfo("basica", "msc4dnc", "songs-a", BasicAdmin, BasicQuery)
            },
            {
                "basicb",
                new SearchServiceInfo("basicb", "msc4dnc", "songs-b", BasicAdmin, BasicQuery)
            },
            {
                "basicc",
                new SearchServiceInfo("basicc", "msc4dnc", "songs-c", BasicAdmin, BasicQuery)
            },
        };
    }
}
