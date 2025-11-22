using DanceLibrary;

namespace m4d.ViewModels;

public sealed class DanceJson(DanceObject d)
{
    public string Id { get; set; } = d.Id;
    public string Name { get; set; } = d.Name;
    public Meter Meter { get; set; } = d.Meter;
    public TempoRange TempoRange { get; set; } = d.TempoRange;
    public decimal TempoDelta { get; set; }
    public string SeoName { get; set; } = d.CleanName;

    public static IEnumerable<DanceJson> Convert(IEnumerable<DanceType> dances)
    {
        return dances.Select(x => new DanceJson(x));
    }
}
