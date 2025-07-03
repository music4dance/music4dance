using System;
using System.Collections.Generic;
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
                {
                    "SongIndexExperimental",
                    new SearchServiceInfo(
                        "SongIndexExperimental", "e", "music4dance", "songs-experimental", credentials)
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
            return [.. _info.Keys];
        }
        public bool HasId(string id)
        {
            return _info.ContainsKey(id);
        }

        public string DefaultId { get; set; }

        public string RawEnvironment { get; }

        private readonly Dictionary<string, SearchServiceInfo> _info;
    }

    public class SearchServiceInfo(string id, string abbr, string name, string index,
        DefaultAzureCredential credentials, bool isStructured = false)
    {
        public string Id { get; } = id;
        public string Abbr { get; } = abbr;
        public string Name { get; } = name;
        public string Index { get; } = index;
        public bool IsStructured { get; } = isStructured;

        public SearchIndexClient SearchIndexClient =>
            new (new Uri($"https://{Name}.search.windows.net"), credentials);

        public SearchClient SearchClient =>
            new(new Uri($"https://{Name}.search.windows.net"), Index, credentials);
    }
}
