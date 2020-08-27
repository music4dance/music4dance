using System.Collections.Generic;
using System.Linq;

namespace m4d.ViewModels
{
    public class HomeModel
    {
        public List<SiteMapEntry> BlogEntries { get; set; }
        public List<DanceClass> Dances { get; set; }

        public HomeModel(IEnumerable<SiteMapCategory> siteMapCategories)
        {
            BlogEntries = siteMapCategories.
                First(c => c.Name == "Info").
                Entries.First(e => e.Title == "Blog").
                Children.ToList();

            Dances = new List<DanceClass>(new[] {
                new DanceClass{Title="Ballroom",Image="ballroom",TopDance="ballroom-competition-categories",
                    Dances=new List<DanceMapping>
                    {
                        new DanceMapping("International Standard"),new DanceMapping("International Latin"),new DanceMapping("American Smooth"),new DanceMapping("American Rhythm")

                    }},
                new DanceClass{Title="Swing",Image="swing",TopDance="swing",
                    Dances=new List<DanceMapping>
                    {new DanceMapping("Lindy Hop"),new DanceMapping("East Coast Swing"),new DanceMapping("West Coast Swing"),new DanceMapping("Hustle"),
                        new DanceMapping("Jive"),new DanceMapping("Jump Swing"), new DanceMapping("Carolina Shag"), new DanceMapping("Collegiate Shag"),
                    }},
                new DanceClass{Title="Latin",Image="salsa",TopDance="latin",
                    Dances=new List<DanceMapping>
                    {
                        new DanceMapping("Salsa"),new DanceMapping("Bachata"),new DanceMapping("Cumbia"),new DanceMapping("Merengue"),
                        new DanceMapping("Mambo"),new DanceMapping("Cha Cha"),new DanceMapping("Rumba"),
                        new DanceMapping("Samba"),new DanceMapping("Bossa Nova")
                    }},
                new DanceClass{Title="Tango",Image="tango",TopDance="tango",
                    Dances=new List<DanceMapping>
                    {
                        new DanceMapping("Argentine Tango"),new DanceMapping("Neo Tango"),new DanceMapping("Milonga"),new DanceMapping("Ballroom Tango","tango-(ballroom)"),
                    }},
                new WeddingDanceClass{Title="Wedding",Image="wedding",TopDance="wedding-music",
                    Dances=new List<DanceMapping>
                    {
                        new WeddingDanceMapping("First Dance"),new WeddingDanceMapping("Mother Son"),new WeddingDanceMapping("Father Daughter"),new WeddingDanceMapping("Last Dance"),
                    }},
            });
        }
    }
}
