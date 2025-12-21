namespace m4dModels;

public interface IDanceStatsManager
{
    DateTime LastUpdate { get; }
    string Source { get; }

    DanceStatsInstance Instance { get; }

    IList<DanceStats> Dances { get; }

    IList<DanceStats> Groups { get; }

    Task ClearCache(DanceMusicCoreService dms, bool fromStore);
    Task ReloadDances(DanceMusicCoreService dms, object serviceHealthManager = null);

    Task<DanceStatsInstance>
        LoadFromAzure(DanceMusicCoreService dms, string source = "default", object serviceHealthManager = null);

    Task Initialize(DanceMusicCoreService dms, object serviceHealthManager = null);

    Task InitializeDanceLibrary();
}


public class DanceStatsManager : IDanceStatsManager
{
    public DanceStatsManager()
    {
    }

    public DanceStatsManager(IDanceStatsFileManager fileManager)
    {
        FileManager = fileManager;
    }

    private IDanceStatsFileManager FileManager { get; }
    public DateTime LastUpdate { get; private set; }
    public string Source { get; private set; }

    #region Access

    public DanceStatsInstance Instance { get; private set; }

    public async Task Initialize(DanceMusicCoreService dms, object serviceHealthManager = null)
    {
        if (Instance != null)
        {
            throw new Exception("Should only Initialize DanceStatsManager once");
        }

        Instance = await LoadFromAppData(dms, serviceHealthManager) ?? await LoadFromAzure(dms, "default", serviceHealthManager);
    }

    public IList<DanceStats> Dances => Instance.Dances;

    public IList<DanceStats> Groups => Instance.Groups;

    #endregion

    #region Building

    public async Task ClearCache(DanceMusicCoreService dms, bool fromStore)
    {
        var instance = fromStore
            ? await LoadFromAzure(dms)
            : await LoadFromAppData(dms, serviceHealthManager: null);

        if (instance != null)
        {
            Instance = instance;
            ClearAssociates();
        }
    }

    private void ClearAssociates()
    {
    }

    public async Task ReloadDances(DanceMusicCoreService dms, object serviceHealthManager = null)
    {
        foreach (var dance in await dms.Context.LoadDances())
        {
            if (Instance.Map.TryGetValue(dance.Id, out var danceStats))
            {
                danceStats.CopyDanceInfo(dance);
            }
        }

        LastUpdate = DateTime.Now;
        Source += " + reload";
    }

    private async Task<DanceStatsInstance> LoadFromAppData(DanceMusicCoreService dms, object serviceHealthManager = null)
    {
        var json = await FileManager.GetStats();
        if (json == null)
        {
            return null;
        }
        await InitializeDanceLibrary();
        LastUpdate = DateTime.Now;
        Source = "AppData";
        Instance = await DanceStatsInstance.LoadFromJson(json, dms, this, serviceHealthManager);
        return Instance;
    }

    public async Task<DanceStatsInstance> LoadFromJson(string json, DanceMusicCoreService dms, object serviceHealthManager = null)
    {
        Source = "Json";
        Instance = await DanceStatsInstance.LoadFromJson(json, dms, this, serviceHealthManager);
        return Instance;
    }

    public async Task<DanceStatsInstance> LoadFromAzure(
        DanceMusicCoreService dms, string source = "default", object serviceHealthManager = null)
    {
        await InitializeDanceLibrary();
        var instance = await DanceStatsInstance.BuildInstance(
            dms, source, serviceHealthManager);

        LastUpdate = DateTime.Now;
        Source = "Azure";
        await SaveToAppData(instance);

        Instance = instance;
        // This will save any tag types that were created via the load from azure

        _ = await dms.GetSongIndex(source).UpdateAzureIndex(null, dms);
        return instance;
    }

    public async Task InitializeDanceLibrary()
    {
        _ = DanceLibrary.Dances.Reset(
            DanceLibrary.Dances.Load(await FileManager.GetDances(), await FileManager.GetGroups()));
    }

    private Task SaveToAppData(DanceStatsInstance instance)
    {
        return FileManager.WriteStats(instance.SaveToJson());
    }
    #endregion
}
