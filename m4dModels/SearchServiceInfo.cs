using System;
using System.Collections.Generic;
using System.Linq;
using Azure;
using Azure.Search.Documents;
using Microsoft.Extensions.Configuration;

namespace m4dModels
{
    public interface ISearchServiceManager
    {
        SearchServiceInfo GetInfo(string id = null);
        string DefaultId { get; set; }
        IEnumerable<string> GetAvailableIds();
        string RawEnvironment { get; }
    }

    public class SearchServiceManager : ISearchServiceManager
    {
        public SearchServiceManager(IConfiguration configuration)
        {
            var basicAuth = new SearchAuth("basic", configuration);
            var backupAuth = new SearchAuth("backup", configuration);
            var freeAuth = new SearchAuth("free", configuration);
            _info = new Dictionary<string, SearchServiceInfo>
            {
                {
                    "basica",
                    new SearchServiceInfo(
                        "basica", "msc4dnc", "songs-a", basicAuth.AdminKey,
                        basicAuth.QueryKey)
                },
                {
                    "basicb",
                    new SearchServiceInfo(
                        "basicb", "msc4dnc", "songs-b", basicAuth.AdminKey,
                        basicAuth.QueryKey /*, isStructured:true */)
                },
                {
                    "basicc",
                    new SearchServiceInfo(
                        "basicc", "msc4dnc", "songs-c", basicAuth.AdminKey,
                        basicAuth.QueryKey)
                },
                {
                    "backup",
                    new SearchServiceInfo(
                        "backup", "m4d-backup", "songs", backupAuth.AdminKey,
                        backupAuth.QueryKey)
                },
                {
                    "freep",
                    new SearchServiceInfo(
                        "freep", "m4d", "pages", freeAuth.AdminKey,
                        freeAuth.QueryKey)
                }
            };

            var env = configuration["SEARCHINDEX"];
            if (string.IsNullOrEmpty(env))
            {
                DefaultId = "basica";
            }
            else
            {
                DefaultId = env;
                RawEnvironment = env;
            }
        }

        public SearchServiceInfo GetInfo(string id = null)
        {
            if (id == null || id == "default")
            {
                id = DefaultId;
            }

            return _info[id];
        }

        public IEnumerable<string> GetAvailableIds()
        {
            return _info.Keys.ToList();
        }


        public string DefaultId { get; set; }

        public string RawEnvironment { get; }

        private readonly Dictionary<string, SearchServiceInfo> _info;
    }

    public class SearchAuth
    {
        public SearchAuth(string name, IConfiguration configuration)
        {
            Name = name;
            _configuration = configuration;
        }

        public string AdminKey => _adminKey ??= GetConfigurationKey("admin");
        public string QueryKey => _queryKey ??= GetConfigurationKey("query");

        private string GetConfigurationKey(string type)
        {
            return _configuration[GetKeyName(type)];
        }

        private string GetKeyName(string type)
        {
            return $"Authentication:AzureSearch:{Name}-{type}";
        }

        private string _adminKey;
        private string _queryKey;

        private readonly IConfiguration _configuration;

        protected string Name { get; }
    }


    public class SearchServiceInfo
    {
        public string Id { get; }
        public string Name { get; }
        public string Index { get; }
        public string AdminKey { get; }
        public string QueryKey { get; }
        public bool IsStructured { get; }

        public SearchServiceInfo(string id, string name, string index, string adminKey,
            string queryKey, bool isStructured = false)
        {
            Id = id;
            Name = name;
            Index = index;
            AdminKey = adminKey;
            QueryKey = queryKey;
            IsStructured = isStructured;
        }

        public SearchClient AdminClient => GetSearchClient(true);
        public SearchClient QueryClient => GetSearchClient(false);

        private SearchClient GetSearchClient(bool admin)
        {
            var endpoint = new Uri($"https://{Name}.search.windows.net");
            var credentials = new AzureKeyCredential(admin ? AdminKey : QueryKey);
            return new SearchClient(endpoint, Index, credentials);
        }

    }
}
