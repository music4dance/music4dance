using Newtonsoft.Json;

namespace DanceLibrary;

public class DanceOrder
{
    public DanceType Dance { get; }
    public decimal Delta { get; }
    public decimal DeltaPercent { get; set; }
    public decimal DeltaPercentAbsolute => Math.Abs(DeltaPercent);
    public decimal DeltaMpm =>
        new Tempo(Delta).Convert(new TempoType(TempoKind.Mpm, Dance.Meter)).Rate;

    public static DanceOrder Create(DanceType dance, decimal tempo)
    {
        return new DanceOrder(
            dance,
            dance.TempoRange.CalculateDelta(tempo),
            dance.TempoRange.CalculateDeltaPercent(tempo));
    }

    private DanceOrder(DanceType dance, decimal tempoDelta, decimal tempoDeltaPercent)
    {
        Dance = dance;
        Delta = tempoDelta;
        DeltaPercent = tempoDeltaPercent;
    }

    public override string ToString()
    {
        var style = Dance.Instances.Count > 0
            ? string.Join(", ", Dance.Instances.Select(inst => inst.Style))
            : "";
        return $"{Dance.Name}: Style=({style}), Delta=({TempoDeltaString})";
    }
    private string TempoDeltaString
    {
        get
        {
            var delta = DeltaMpm;
            return Math.Abs(delta) < .01M
                ? ""
                : delta < 0 ? $"{delta:F2}MPM" : $"+{delta:F2}MPM";
        }
    }
}
