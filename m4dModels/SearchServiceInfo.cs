using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;

namespace m4dModels
{
    public interface ISearchServiceManager
    {
        SearchServiceInfo GetInfo(string id = null);
        string DefaultId { get; set; }
        IEnumerable<string> GetAvailableIds();
        string RawEnvironment { get; }
        int CodeVersion { get; }
        int ConfigVersion { get; set; }
        bool NextVersion { get; } 
        bool HasNextVersion { get; }
        bool HasId(string id);
        SongFilter GetSongFilter(string filter = null);
        SongFilter GetSongFilter(RawSearch raw, string action = null);
        void RedirectToUpdate();
    }

    public class SearchServiceManager : ISearchServiceManager
    {
        public static readonly string ExperimentalId = "SongIndexExperimental";

        public SearchServiceManager(IConfiguration configuration, 
            IAzureClientFactory<SearchClient> searchFactory,
            IAzureClientFactory<SearchIndexClient> searchIndexFactory)
        {
            _info = [];

            foreach (var section in configuration.GetChildren()
                .Where(s => s.GetChildren().Any(
                    child => child.Key.Equals("indexname", StringComparison.OrdinalIgnoreCase) && 
                    child.Value.StartsWith("songs-"))))
            {
                var parts = section.Key.Split('-');
                var baseName = parts[0].Trim();
                var ver = parts.Length > 1 ? int.Parse(parts[1]) : CodeVersion + 1;
                var indexName = section["indexname"];

                if (_info.TryGetValue(baseName, out var existingInfo))
                {
                    existingInfo.AddVersion(ver, indexName);
                }
                else
                {
                    _info[baseName] = new SearchServiceInfo(
                        baseName, ver, indexName, searchFactory, searchIndexFactory, this);
                }

            }
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

            var versionString = configuration["SEARCHINDEXVERSION"];
            if (string.IsNullOrEmpty(env) || !int.TryParse(versionString, out var version))
            {
                ConfigVersion = CodeVersion;
            }
            else
            {
                ConfigVersion = version;
            }
            if (DefaultId == ExperimentalId)
            {
                ConfigVersion += 1;
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

        public SongFilter GetSongFilter(string filter = null)
        {
            return SongFilter.Create(NextVersion, filter);
        }

        public SongFilter GetSongFilter(RawSearch raw, string action = null)
        {
            return SongFilter.Create(NextVersion, raw, action);
        }

        public string DefaultId
        {
            get => _defaultId; set
            {
                if (_defaultId == ExperimentalId)
                {
                    ConfigVersion -= 1;
                }
                _defaultId = value;
                if (value == ExperimentalId)
                {
                    ConfigVersion += 1;
                }
            }
        }
        public int CodeVersion => 2;
        public bool HasNextVersion =>
          CodeVersion == ConfigVersion &&
          GetInfo().HasNextVersion;           

        public int ConfigVersion { get; set; }

        public bool NextVersion => ConfigVersion > CodeVersion;

        public string RawEnvironment { get; }
        public void RedirectToUpdate()
        {
            ConfigVersion = CodeVersion + 1;
        }

        private readonly Dictionary<string, SearchServiceInfo> _info;
        private string _defaultId;
    }

    public class SearchServiceInfo(string id, int version, string name,
        IAzureClientFactory<SearchClient> clientFactory, IAzureClientFactory<SearchIndexClient> clientIndexFactory,
        ISearchServiceManager manager)
    {
        public string Id { get; } = id;
        public string Abbr
        {
            get
            {
                if (string.IsNullOrEmpty(Id))
                    return string.Empty;
                var lastUpper = Id.Reverse().FirstOrDefault(char.IsUpper);
                return lastUpper != default
                    ? lastUpper.ToString().ToLowerInvariant()
                    : Id[..1].ToLowerInvariant();
            }
        }

        public void AddVersion(int version, string name)
        {
            _versionedNames[version] = name;
        }

        public SearchClient GetSearchClient(bool isNext) =>
            clientFactory.CreateClient(GetVersionedId(isNext));

        private SearchIndexClient GetSearchIndexClient(bool isNext) =>
            clientIndexFactory.CreateClient("SongIndex");

        public async Task<SearchIndex> GetIndexAsync(bool isNext)
        {
            return await GetSearchIndexClient(isNext)
                .GetIndexAsync(GetVersionedName(isNext));
        }

        public SearchIndex BuildIndex(
            List<SearchField> fields,
            IList<SearchSuggester> suggesters = null,
            IList<ScoringProfile> scoringProfiles = null,
            string defaultScoringProfile = null,
            bool? isNext = null
            ) {
            var index = new SearchIndex(GetVersionedName(isNext), fields);
            index.Suggesters.AddRange(suggesters ?? []);
            index.ScoringProfiles.AddRange(scoringProfiles ?? []);
            index.DefaultScoringProfile = defaultScoringProfile;
            return index;
        }

        public async Task<Response> DeleteIndexAsync(bool isNext)
        {
            var client = GetSearchIndexClient(isNext);
            return await client.DeleteIndexAsync(GetVersionedName(isNext));
        }

        public async Task<Response<SearchIndex>> CreateIndexAsync(SearchIndex index, bool isNext)
        {
            var client = GetSearchIndexClient(isNext);
            return await client.CreateIndexAsync(index);
        }

        public async Task<Response<SearchIndex>> CreateOrUpdateIndexAsync(SearchIndex index, bool isNext)
        {
            var client = GetSearchIndexClient(isNext);
            return await client.CreateOrUpdateIndexAsync(index);
        }
        public bool HasNextVersion => _versionedNames.Count > 1;

        private string GetVersionedId(bool? isNext = null) =>
            Id == SearchServiceManager.ExperimentalId
                ? Id 
                : $"{Id}-{(isNext ?? manager.NextVersion ? manager.CodeVersion + 1 : manager.ConfigVersion)}";

        private string GetVersionedName(bool? isNext = null) =>
            _versionedNames[isNext ?? manager.NextVersion ? manager.CodeVersion + 1 : manager.ConfigVersion];

        private readonly Dictionary<int, string> _versionedNames = new()
        {
            { version, name }
        };
    }
}
