using System;
using System.Collections.Generic;
using System.Linq;
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;

namespace m4dModels
{
    public interface ISearchServiceManager
    {
        SearchServiceInfo GetInfo(string id = null);
        string DefaultId { get; set; }
        IEnumerable<string> GetAvailableIds();
        string RawEnvironment { get; }
        bool HasId(string id);
    }

    public class SearchServiceManager : ISearchServiceManager
    {
        public SearchServiceManager(IConfiguration configuration, DefaultAzureCredential credentials)
        {
            var basicAuth = new SearchAuth("basic", configuration);
            var backupAuth = new SearchAuth("backup", configuration);
            var freeAuth = new SearchAuth("free", configuration);
            _info = new Dictionary<string, SearchServiceInfo>
            {
                {
                    "SongIndexProd",
                    new SearchServiceInfo(
                        "SongIndexProd", "p", "music4dance", "songs-prod", credentials)
                },
                {
                    "SongIndexTest",
                    new SearchServiceInfo(
                        "SongIndexTest", "t", "music4dance", "songs-test", credentials)
                },
            };

            var env = configuration["SEARCHINDEX"];
            if (string.IsNullOrEmpty(env))
            {
                DefaultId = "SongIndexProd";
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
        public bool HasId(string id)
        {
            return _info.ContainsKey(id);
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
        public string Abbr { get; }
        public string Name { get; }
        public string Index { get; }
        public string AdminKey { get; }
        public string QueryKey { get; }
        public bool IsStructured { get; }

        private DefaultAzureCredential _credentials;

        public SearchServiceInfo(string id, string abbr, string name, string index,
            string adminKey, string queryKey, bool isStructured = false)
        {
            Id = id;
            Abbr = abbr;
            Name = name;
            Index = index;
            AdminKey = adminKey;
            QueryKey = queryKey;
            IsStructured = isStructured;
        }

        public SearchServiceInfo(string id, string abbr, string name, string index,
            DefaultAzureCredential credentials, bool isStructured = false)
        {
            Id = id;
            Abbr = abbr;
            Name = name;
            Index = index;
            _credentials = credentials;
            IsStructured = isStructured;
        }

        public SearchClient AdminClient => GetSearchClient(true);
        public SearchClient QueryClient => GetSearchClient(false);

        public SearchIndexClient GetSearchIndexClient()
        {
            var endpoint = new Uri($"https://{Name}.search.windows.net");
            if (_credentials == null)
            {
                return new SearchIndexClient(endpoint, new AzureKeyCredential(AdminKey));
            }
            else
            {
                return new SearchIndexClient(endpoint, _credentials);
            }
        }

        private SearchClient GetSearchClient(bool admin)
        {
            var endpoint = new Uri($"https://{Name}.search.windows.net");
            if (_credentials == null)
            {
                return new SearchClient(endpoint, Index, new AzureKeyCredential(admin ? AdminKey : QueryKey));
            }
            else
            {
                return new SearchClient(endpoint, Index, _credentials);
            }
        }

    }
}
