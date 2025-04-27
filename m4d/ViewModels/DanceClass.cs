using DanceLibrary;

using Newtonsoft.Json;

namespace m4d.ViewModels;

public class DanceMapping(string title = null, string name = null)
{
    public virtual string Name { get; } = name ?? DanceObject.SeoFriendly(title);
    public virtual string Title { get; } = title;

    public virtual string Controller => "dances";

    public virtual string QueryString => Parameters == null ? null : $"filter={Parameters}";

    [JsonIgnore]
    public virtual string Parameters => null;
}

public class DanceClass
{
    public List<DanceMapping> Dances;
    public string Image;
    public string Title;
    public string TopDance;
    public virtual string FullTitle => "Music for " + Title + " Dancers";
}

public class WeddingDanceClass : DanceClass
{
    public override string FullTitle => "Music for Wedding Dances";
}

public class WeddingDanceMapping(string tag) : DanceMapping
{
    public override string Title => tag.Contains("Dance") ? tag : tag.Replace(' ', '/');

    public override string Name => "index";
    public override string Controller => "song";

    public override string Parameters => $"Index-.-.-.-.-.-.-.-1-+{tag}:Other";
}
