using System.Collections.Generic;
using DanceLibrary;
using Newtonsoft.Json;

namespace m4d.ViewModels
{
    public class DanceMapping
    {
        public DanceMapping(string title = null, string name = null)
        {
            Title = title;
            Name = name ?? DanceObject.SeoFriendly(title);
        }

        public virtual string Name { get; }
        public virtual string Title { get; }

        public virtual string Controller => "dances";

        [JsonIgnore]
        public virtual string Parameters => null;
    }

    public class DanceClass
    {
        public List<DanceMapping> Dances;
        public string Image;
        public string Title;
        public string TopDance;
    }

    public class WeddingDanceClass : DanceClass
    {
    }

    public class WeddingDanceMapping : DanceMapping
    {
        private readonly string _tag;

        public WeddingDanceMapping(string tag)
        {
            _tag = tag;
        }

        public override string Title => _tag.Contains("Dance") ? _tag : _tag.Replace(' ', '/');

        public override string Name => "index";
        public override string Controller => "song";

        public override string Parameters => $"Index-.-.-.-.-.-.-.-1-+{_tag}:Other";
    }
}
