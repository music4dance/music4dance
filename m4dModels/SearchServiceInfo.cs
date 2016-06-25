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

        public static SearchServiceInfo GetInfo(string id=null)
        {
            if (id == null || id == "default")
            {
                id = DefaultId;
            }

            return s_info[id];
        }

        public static bool UseSql
        {
            get
            {
                LoadDefault();
                return s_useSql;
            }
            set
            {
                LoadDefault();
                s_useSql = value;
            }
        }

        //private const string FreeAdmin = "***REMOVED***";
        //private const string FreeQuery = "5B2BAFC30F0CD25405A10B08582B5451";

        private const string BasicAdmin = "***REMOVED***";
        private const string BasicQuery = "***REMOVED***";

        public static string DefaultId
        {
            get
            {
                return LoadDefault();
            }
            set
            {
                s_defaultId = value;
            }
        }

        private static string LoadDefault()
        {
            if (s_defaultId != null) return s_defaultId;

            var env = Environment.GetEnvironmentVariable("SEARCHINDEX");
            if (env == null)
            {
                s_useSql = false;
                return s_defaultId = "basica";
            }

            s_env = env;
            var rg = env.Split('-');
            if (rg.Length <= 1 || rg[1] != "SQL")
            {
                s_useSql = false;
            }
            return s_defaultId = rg[0];
        }

        public static string RawEnvironment
        {
            get
            {
                LoadDefault();
                return s_env;
            }
        }

        private static string s_defaultId;
        private static bool s_useSql;
        private static string s_env = "(EMPTY)";

        private static readonly Dictionary<string, SearchServiceInfo> s_info = new Dictionary<string, SearchServiceInfo>
        {
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
