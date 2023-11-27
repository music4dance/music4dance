namespace DanceLibrary;

public class OrgSpec
{
    public string Name { get; set; } // NDCA or DanceSport
    public string Category { get; set; } // Level or Competitor or NULL

    public string
        Qualifier
    {
        get;
        set;
    } // Level = Bronze or Silver,Gold; Competitor = Professional,Amateur or ProAm

    public string Title
    {
        get
        {
            var title = "All Organizations";
            if (Name != "All")
            {
                title = Name;
                if (Category != null)
                {
                    title += " (" + Qualifier + ")";
                }
            }

            return title;
        }
    }
}
