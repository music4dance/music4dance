using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

// NEXTSTEPS:
//  audit use of static DanceLibrary.Instance
//  Reconsider FilterDances - Should it build a new Dances object?
//    If so we also need to rework Reduces so that FilterDances would be
//    idempotent if it didn't filter anything (and generally let us
//    do incremental filtering without loss of information).
namespace DanceLibrary;

internal static class Tags
{
    internal static readonly string Style = "Style";
    internal static readonly string All = "All";
    internal static readonly string Organization = "Organization";
}

public enum DanceCategoryType
{
    Both,
    American,
    International
}

public class Dances
{
    private static readonly JsonSerializerSettings s_settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore
    };

    private static readonly object s_lock = new();
    private static Dances s_instance;
    private readonly List<DanceInstance> _allDanceInstances = [];
    private readonly List<DanceObject> _allDanceObjects = [];

    private readonly Dictionary<string, DanceObject> _danceDictionary = [];

    private readonly List<DanceType> _npDanceTypes = [];
    private List<DanceGroup> _allDanceGroups = [];

    private List<DanceType> _allDanceTypes = [];

    private Dances()
    {
    }


    public IEnumerable<DanceInstance> AllDanceInstances => _allDanceInstances;

    public IEnumerable<DanceObject> AllDances => _allDanceObjects;

    public IEnumerable<DanceType> AllDanceTypes => _allDanceTypes;

    public IEnumerable<DanceType> NonPerformanceDanceTypes => _npDanceTypes;

    public IEnumerable<DanceGroup> AllDanceGroups => _allDanceGroups;

    public static Dances Instance => s_instance;

    private HashSet<string> _allDanceWordsUpperCache;

    /// <summary>
    /// Gets a flat list of all dance names and synonyms in uppercase for remix detection.
    /// Includes both full names and significant words (fragments) from multi-word names.
    /// Cached for performance since this is called frequently during merge operations.
    /// </summary>
    public HashSet<string> GetAllDanceWordsUpper()
    {
        if (_allDanceWordsUpperCache != null)
        {
            return _allDanceWordsUpperCache;
        }

        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect all names and synonyms
        var allNamesAndSynonyms = new List<string>();
        foreach (var dance in AllDances)
        {
            allNamesAndSynonyms.Add(dance.Name);
            
            if (dance.Synonyms != null)
            {
                allNamesAndSynonyms.AddRange(dance.Synonyms.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }

        // Process all names and synonyms together
        foreach (var name in allNamesAndSynonyms)
        {
            // Add the full name/synonym
            words.Add(name.ToUpperInvariant());

            // Split into words and add significant ones
            var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in nameWords)
            {
                // Only add words that are likely dance names (not common words like "Dance", "The", etc.)
                if (word.Length > 3 && !word.Equals("DANCE", StringComparison.OrdinalIgnoreCase))
                {
                    words.Add(word.ToUpperInvariant());
                }
            }
        }

        _allDanceWordsUpperCache = words;
        return _allDanceWordsUpperCache;
    }

    public IEnumerable<DanceObject> ExpandGroups(IEnumerable<DanceObject> dances)
    {
        var expanded = new List<DanceObject>();
        foreach (var d in dances)
        {
            switch (d)
            {
                case DanceType _:
                    expanded.Add(d);
                    break;
                case DanceGroup group:
                    expanded.AddRange(group.Members);
                    break;
            }
        }

        return expanded;
    }

    private void LoadDances(List<DanceType> danceTypes)
    {
        _allDanceTypes = danceTypes;
        foreach (var dt in _allDanceTypes)
        {
            _allDanceInstances.AddRange(dt.Instances);
        }

        foreach (var dt in _allDanceTypes)
        {
            _allDanceObjects.Add(dt);
            _danceDictionary.Add(dt.Id, dt);
            if (dt.Instances.All(di => di.StyleId != 'P'))
            {
                _npDanceTypes.Add(dt);
            }
        }

        CompetitionCategory.Reset();
        foreach (var di in _allDanceInstances)
        {
            _allDanceObjects.Add(di);
            _danceDictionary.Add(di.Id, di);
            CompetitionCategory.RegisterDanceInstance(di);
        }
    }

    private void LoadGroups(List<DanceGroup> danceGroups)
    {
        _allDanceGroups = danceGroups;

        foreach (var dg in _allDanceGroups)
        {
            _allDanceObjects.Add(dg);
            _danceDictionary.Add(dg.Id, dg);
            dg.Members = FromIds(dg.DanceIds);
            foreach (var member in dg.Members.Where(m => m is DanceType))
            {
                (member as DanceType).Groups.Add(dg);
            }
        }
    }

    public DanceObject DanceFromName(string name)
    {
        return string.IsNullOrEmpty(name)
            ? null
            : _allDanceObjects.FirstOrDefault(
            d =>
                string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public DanceObject DanceFromId(string id)
    {
        return string.IsNullOrEmpty(id) ? null : _danceDictionary.TryGetValue(id.ToUpper(), out var ret) ? ret : null;
    }

    public string GetJSON()
    {
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        var json = JsonConvert.SerializeObject(_allDanceTypes, Formatting.Indented, settings);

        return json;
    }

    public IEnumerable<DanceOrder> FilterDances(DanceFilter filter, Tempo tempo, decimal epsilon)
    {
        return filter
            .Filter(_allDanceTypes)
            .Select(dance => DanceOrder.Create(dance, tempo.BeatsPerMinute))
            .Where(order => order.DeltaPercentAbsolute < epsilon)
            .OrderBy(order => order.DeltaPercentAbsolute);
    }

    public IList<DanceObject> FromIds(IEnumerable<string> dances)
    {
        var dos = new List<DanceObject>();
        if (dances == null)
        {
            return dos;
        }

        foreach (var s in dances)
        {
            if (_danceDictionary.TryGetValue(s.ToUpper(), out var d))
            {
                dos.Add(d);
            }
        }

        return dos;
    }

    public IList<DanceObject> FromNames(IEnumerable<string> dances)
    {
        var dos = new List<DanceObject>();
        if (dances == null)
        {
            return dos;
        }

        dos.AddRange(
            dances
                .Select(
                    s => AllDances.FirstOrDefault(
                        d =>
                            string.Equals(s, d.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(dobj => dobj != null));

        return dos;
    }


    private static List<T> LoadFromJson<T>(string json)
    {
        return JsonConvert.DeserializeObject<List<T>>(json, s_settings);
    }

    public static Dances Load(string typesJson, string groupsJson)
    {
        return Load(
            LoadFromJson<DanceType>(typesJson),
            LoadFromJson<DanceGroup>(groupsJson));
    }

    public static Dances Load(List<DanceType> danceTypes, List<DanceGroup> danceGroups)
    {
        var dances = new Dances();

        dances.LoadDances(danceTypes);
        dances.LoadGroups(danceGroups);

        return dances;
    }

    public static Dances Reset(Dances instance)
    {
        lock (s_lock)
        {
            return s_instance = instance;
        }
    }
}
