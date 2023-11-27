using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace DanceLibrary;

public class DanceInstance : DanceObject
{
    [JsonConstructor]
    public DanceInstance(string style, TempoRange tempoRange, DanceException[] exceptions)
    {
        Style = style;
        TempoRange = tempoRange;
        Exceptions = exceptions == null ? new List<DanceException>() : new List<DanceException>(exceptions);
    }

    [JsonIgnore]
    public DanceType DanceType { get; internal set; }

    public sealed override TempoRange TempoRange { get; set; }

    public override string Id => DanceType.Id + StyleId;

    public override Meter Meter => DanceType.Meter;

    public override string Name => ShortStyle + ' ' + DanceType.Name;

    public string Style { get; set; }

    public string CompetitionGroup { get; set; }

    [DefaultValue(0)]
    public int CompetitionOrder { get; set; }

    public List<DanceException> Exceptions { get; set; }

    [JsonIgnore]
    public TempoRange FilteredTempo
    {
        get
        {
            var exceptions = GetFilteredExceptions();

            // Include the general tempo iff the exceptions don't fully cover the
            //  selected filters for the instance in question
            TempoRange tempoRange = null;
            if (IncludeGeneral(exceptions))
            {
                tempoRange = TempoRange;
            }

            // Now include all of the tempos in the exceptions that are covered by
            //  the selected filter

            return exceptions.Aggregate(
                tempoRange,
                (current, de) => de.TempoRange.Include(current));
        }
    }

    [JsonIgnore]
    public string ShortStyle
    {
        get
        {
            var words = Style.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Debug.Assert(words.Length > 0);
            return words[0];
        }
    }

    [JsonIgnore]
    public char StyleId
    {
        get
        {
            var ss = ShortStyle;
            Debug.Assert(!string.IsNullOrEmpty(ss));
            return ShortStyle[0];
        }
    }

    private string MergeInclusion(string oldInc, string newInc)
    {
        var ret = oldInc;

        if (newInc == Tags.All)
        {
            ret = Tags.All;
        }
        else if (string.IsNullOrEmpty(oldInc))
        {
            ret = newInc;
        }
        else if (!string.Equals(oldInc, newInc))
        {
            ret = oldInc + "," + newInc;
        }

        return ret;
    }

    private bool IncludeGeneral(ReadOnlyCollection<DanceException> exceptions)
    {
        // No exceptions, so definitely need general
        if (exceptions.Count == 0)
        {
            return true;
        }

        var competitors = "";
        var levels = "";
        var orgs = "";

        foreach (var de in exceptions)
        {
            competitors = MergeInclusion(competitors, de.Competitor);
            levels = MergeInclusion(levels, de.Level);
            orgs = MergeInclusion(orgs, de.Organization);
        }

        return !FilterObject.IsCovered(orgs, competitors, levels);
    }

    private ReadOnlyCollection<DanceException> GetFilteredExceptions()
    {
        var exceptions = new List<DanceException>();

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var de in Exceptions)
        {
            if (FilterObject.GetValue(Tags.Competitor, de.Competitor) &&
                FilterObject.GetValue(Tags.Level, de.Level) &&
                FilterObject.GetValue(Tags.Organization, de.Organization))
            {
                exceptions.Add(de);
            }
        }

        return new ReadOnlyCollection<DanceException>(exceptions);
    }

    public bool CalculateTempoMatch(decimal tempo, decimal epsilon, out decimal delta,
        out decimal deltaPercent, out decimal median)
    {
        var ret = false;
        var filteredTempo = new TempoRange(FilteredTempo.Min / Meter.Numerator, FilteredTempo.Max / Meter.Numerator);
        delta = filteredTempo.CalculateDelta(tempo);
        median = (filteredTempo.Min + filteredTempo.Max) / 2;
        deltaPercent = delta * 100 / median;

        // First check to see if the instance in general matches
        if (Math.Abs(deltaPercent) < epsilon)
            // Then see if any of the exception filters fire
        {
            ret = true;
        }

        return ret;
    }

    public bool CalculateBeatMatch(decimal tempo, decimal epsilon, out decimal delta,
        out decimal deltaPercent, out decimal median)
    {
        var b = new Tempo(tempo, new TempoType(TempoKind.Bpm)); // Tempo in beats per minute
        var t = b.Convert(new TempoType(TempoKind.Mpm, Meter));

        return CalculateTempoMatch(t.Rate, epsilon, out delta, out deltaPercent, out median);
    }

    /// <summary>
    ///     Does some basic filtering against absolute (non-tempo based) filters
    ///     If Meter doesn't match, this dance won't work
    ///     If Style doesn't match, we won't get a valid result
    ///     If Orginization/Level/Competitor are null it doesn't make sense, so fail
    /// </summary>
    /// <param name="meter"></param>
    /// <returns></returns>
    public bool CanMatch(Meter meter)
    {
        // Meter is an absolute match
        if (!DanceType.Meter.Equals(meter))
        {
            return false;
        }

        // Style is an absolute match
        if (!FilterObject.GetValue(Tags.Style, Style))
        {
            return false;
        }

        // If no originizations are checked, we can't match
        if (!FilterObject.GetValue(Tags.Organization, Tags.All))
        {
            return false;
        }

        // If NDCA only is checked and either Level or Competitor empty we can't match
        if (!FilterObject.GetValue(Tags.Organization, "DanceSport"))
        {
            if (!FilterObject.GetValue(Tags.Competitor, Tags.All) ||
                !FilterObject.GetValue(Tags.Level, Tags.All))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString()
    {
        return $"{Style} ({FilteredTempo}BPM)";
    }
}
