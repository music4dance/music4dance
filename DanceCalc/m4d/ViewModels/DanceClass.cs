using System.Collections.Generic;
using DanceLibrary;

namespace m4d.ViewModels
{
    public class DanceMapping
    {
        public DanceMapping(string title = null, string name = null)
        {
            Title = title;
            Name = name ?? DanceObject.SeoFriendly(title);
        }

        public string Name;
        public string Title;
    }
    public class DanceClass
    {
        public string Title;
        public string Image;
        public string TopDance;

        public List<DanceMapping> Dances;
    }
}