using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace DanceLibrary;

public class DanceSample : IComparable<DanceSample>
{
    private readonly List<DanceInstance> _rgdi = new();

    public DanceSample(DanceInstance di, decimal delta)
    {
        _rgdi.Add(di);
        TempoDelta = delta;
    }

    public DanceType DanceType => _rgdi[0].DanceType;

    public string Style
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var di in _rgdi)
            {
                sb.Append(di.Style);
                sb.Append(", ");
            }

            sb.Remove(sb.Length - 2, 2);
            return sb.ToString();
        }
    }

    public decimal TempoDelta { get; set; }

    public string TempoDeltaString
    {
        get
        {
            return Math.Abs(TempoDelta) < .01M 
                ? "" 
                : TempoDelta < 0 ? $"{TempoDelta:F2}MPM" : $"+{TempoDelta:F2}MPM";
        }
    }

    public decimal TempoDeltaPercent { get; set; }

    public ReadOnlyCollection<DanceInstance> Instances =>
        new(_rgdi);

    public int CompareTo(DanceSample other)
    {
        return Math.Abs(TempoDelta).CompareTo(Math.Abs(other.TempoDelta));
    }

    public void Add(DanceInstance di)
    {
        _rgdi.Add(di);
    }

    public override string ToString()
    {
        return $"{DanceType.Name}: Style=({Style}), Delta=({TempoDeltaString})";
    }
}
