using System.Collections.Generic;
using System.Linq;

namespace DanceLibrary;

public class CompetitionGroup
{
    public string Name { get; set; }

    public List<CompetitionCategory> Categories { get; set; }

    public static CompetitionGroup Get(string name)
    {
        return new CompetitionGroup
        {
            Name = name,
            Categories = [.. CompetitionCategory.GetCategoryList(name)]
        };
    }
}
