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
            BlogEntries = siteMapCategories.First(c => c.Name == "Info").Entries
                .First(e => e.Title == "Blog").Children.Where(e => !e.OneTime).ToList();

            Dances = new List<DanceClass>(
                new[]
                {
                    new DanceClass
                    {
                        Title = "Ballroom", Image = "ballroom",
                        TopDance = "ballroom-competition-categories",
                        Dances = new List<DanceMapping>
                        {
                            new("International Standard"), new("International Latin"),
                            new("American Smooth"), new("American Rhythm")
                        }
                    },
                    new DanceClass
                    {
                        Title = "Swing", Image = "swing", TopDance = "swing",
                        Dances = new List<DanceMapping>
                        {
                            new("Lindy Hop"), new("East Coast Swing"), new("West Coast Swing"),
                            new("Hustle"),
                            new("Jive"), new("Jump Swing"), new("Carolina Shag"),
                            new("Collegiate Shag")
                        }
                    },
                    new DanceClass
                    {
                        Title = "Latin", Image = "salsa", TopDance = "latin",
                        Dances = new List<DanceMapping>
                        {
                            new("Salsa"), new("Bachata"), new("Cumbia"), new("Merengue"),
                            new("Mambo"), new("Cha Cha"), new("Rumba"),
                            new("Samba"), new("Bossa Nova")
                        }
                    },
                    new DanceClass
                    {
                        Title = "Tango", Image = "tango", TopDance = "tango",
                        Dances = new List<DanceMapping>
                        {
                            new("Argentine Tango"), new("Neo Tango"), new("Milonga"),
                            new("Ballroom Tango", "tango-(ballroom)")
                        }
                    },
                    new WeddingDanceClass
                    {
                        Title = "Wedding", Image = "wedding", TopDance = "wedding-music",
                        Dances = new List<DanceMapping>
                        {
                            new WeddingDanceMapping("First Dance"),
                            new WeddingDanceMapping("Mother Son"),
                            new WeddingDanceMapping("Father Daughter"),
                            new WeddingDanceMapping("Last Dance")
                        }
                    }
                });
        }
    }
}
