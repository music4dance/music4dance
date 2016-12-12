using System;
using System.Collections.Generic;
using System.Linq;

namespace m4dModels
{
    public class SearchAuth
    {
        public SearchAuth(string name)
        {
            Name = name;
        }

        public string AdminKey => _adminKey ?? (_adminKey = Environment.GetEnvironmentVariable(Name + "-admin"));
        public string QueryKey => _queryKey ?? (_queryKey = Environment.GetEnvironmentVariable(Name + "-query"));

        private string _adminKey;
        private string _queryKey;

        protected string Name { get; }
    }

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

        public static IEnumerable<string> GetAvailableIds()
        {
            return s_info.Keys.ToList();
        }



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
                return s_defaultId = "basicb";
            }

            s_env = env;
            return s_defaultId = s_env;
        }

        public static string RawEnvironment
        {
            get
            {
                LoadDefault();
                return s_env;
            }
        }

        static SearchServiceInfo()
        {
            var basicAuth = new SearchAuth("basic");
            var backupAuth = new SearchAuth("backup");
            s_info = new Dictionary<string, SearchServiceInfo>
                        {
            {
                "basica",
                new SearchServiceInfo("basica", "msc4dnc", "songs-a", basicAuth.AdminKey, basicAuth.QueryKey)
            },
            {
                "basicb",
                new SearchServiceInfo("basicb", "msc4dnc", "songs-b", basicAuth.AdminKey, basicAuth.QueryKey)
            },
            {
                "basicc",
                new SearchServiceInfo("basicc", "msc4dnc", "songs-c", basicAuth.AdminKey, basicAuth.QueryKey)
            },
            {
                "backup",
                new SearchServiceInfo("backup", "m4d-backup", "songs", backupAuth.AdminKey, backupAuth.QueryKey)
            },
        };

        }

        private static string s_defaultId;
        private static string s_env = "(EMPTY)";

        private static readonly Dictionary<string, SearchServiceInfo> s_info;
    }
}
