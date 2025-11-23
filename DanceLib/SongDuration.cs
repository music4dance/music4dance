using System.Diagnostics;

namespace DanceLibrary;

public enum DurationFormat
{
    Short,
    Long
}

/// <summary>
///     Representation of the duration of a song
///     This is really just a wrapper around a decimal number of seconds
///     that allows easy formatting and storage
///     This is an immutable structure
///     TODO: Figure out what I was thinking about with durations of beats and measures, did
///     this get solved in another way (in which case I should clean up this class)
/// </summary>
public readonly struct SongDuration : IComparable<SongDuration>
{
    #region Constructors

    /// <summary>
    ///     Create a song duration with a number of seconds
    /// </summary>
    /// <param name="length">number of seconds</param>
    public SongDuration(decimal length) : this()
    {
        Length = length;
        Validate();
    }

    /// <summary>
    ///     Create a song with any arbitrary tempo
    /// </summary>
    /// <param name="length">minutes, seconds, beats or measures depending on type</param>
    /// <param name="type">any duration type</param>
    /// <param name="tempo">any duration type</param>
    public SongDuration(decimal length, DurationType type, Tempo tempo)
        : this()
    {
        switch (type.DurationKind)
        {
            case DurationKind.Second:
                Length = length;
                break;
            case DurationKind.Minute:
                Length = length * 60;
                break;
            case DurationKind.Beat:
                ArgumentNullException.ThrowIfNull(tempo);

                Length = length * tempo.SecondsPerBeat;
                break;
            case DurationKind.Measure:
                ArgumentNullException.ThrowIfNull(tempo);

                Length = length * tempo.SecondsPerMeasure;
                break;
            default:
                Debug.Assert(false);
                break;
        }

        Validate();
    }

    public SongDuration(decimal length, DurationType type)
        : this(length, type, null)
    {
    }

    /// <summary>
    ///     Create a song duration from a string (currently only parsing 'short' version of syntax
    ///     MmSs
    /// </summary>
    public SongDuration(string s)
        : this()
    {
        var imin = s.IndexOf('m');
        var isec = s.IndexOf('s');

        var smin = string.Empty;
        var ssec = string.Empty;

        if (imin < 0 && isec < 0)
        {
            if (s.Contains(':'))
            {
                var parts = s.Split([':'], StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 3)
                {
                    smin = parts[1];
                    ssec = parts[2];
                }
                else if (parts.Length == 2)
                {
                    smin = parts[0];
                    ssec = parts[1];
                }
                else if (parts.Length == 1)
                {
                    ssec = parts[0];
                }
            }
            else
            {
                ssec = s;
            }
        }
        else
        {
            if (imin < 0)
            {
                ssec = s[..isec];
            }
            else if (isec < 0)
            {
                smin = s[..imin];
            }
            else
            {
                smin = s[..imin];
                ssec = s.Substring(imin + 1, isec);
            }
        }

        decimal seconds = 0;
        if (!string.IsNullOrEmpty(smin) && decimal.TryParse(smin, out var dmin))
        {
            seconds = 60 * dmin;
        }

        if (!string.IsNullOrEmpty(ssec) && decimal.TryParse(ssec, out var dsec))
        {
            seconds += dsec;
        }

        Length = seconds;

        Validate();
    }

    private void Validate()
    {
        if (Length < 0)
        {
            throw new ArgumentOutOfRangeException("length", "length must be non-negative");
        }
    }

    #endregion

    #region Properties

    public decimal Length { get; }

    public int Minutes => (int)(Length / 60);

    public int Seconds => (int)(Length - 60 * Minutes);

    #endregion

    #region operators

    public static implicit operator decimal(SongDuration sd)
    {
        return sd.Length;
    }

    public static implicit operator SongDuration(decimal d)
    {
        return new SongDuration(d);
    }

    public int CompareTo(SongDuration other)
    {
        return Length.CompareTo(other.Length);
    }


    public override bool Equals(object obj)
    {
        return obj is SongDuration other && CompareTo(other) == 0;
    }

    public override int GetHashCode()
    {
        return Length.GetHashCode();
    }

    public static bool operator ==(SongDuration a, SongDuration b)
    {
        return a.Length == b.Length;
    }

    public static bool operator !=(SongDuration a, SongDuration b)
    {
        return a.Length != b.Length;
    }

    public decimal LengthIn(DurationKind dk, Tempo tempo = null)
    {
        switch (dk)
        {
            case DurationKind.Second:
                return Length;
            case DurationKind.Minute:
                return Length / 60;
            case DurationKind.Beat:
                ArgumentNullException.ThrowIfNull(tempo);

                return Length / tempo.SecondsPerBeat;
            case DurationKind.Measure:
                ArgumentNullException.ThrowIfNull(tempo);

                return Length / tempo.SecondsPerMeasure;
            default:
                Debug.Assert(false);
                return 0M;
        }
    }

    #endregion

    #region formatting

    public override string ToString()
    {
        var ret = Format(DurationFormat.Short);
        return ret;
    }

    public string ToString(string format)
    {
        var c = 'M';

        if (format.Length > 0)
        {
            c = format[0];
        }

        return c switch
        {
            'S' => Format(DurationFormat.Short),
            'L' => Format(DurationFormat.Long),
            _ => $"{Minutes}:{Seconds:D2}",
        };
    }

    public string Format(DurationFormat f)
    {
        string[] rgs = ["{0:N0}s", "{0}m", "{0}m{1}s"];
        string[] rgl = ["{0:N0} second(s)", "{0} minute(s)", "{0} minutes, {1} seconds"];
        var rg = rgl;

        var exact = false;
        if (Length / 60 == Minutes)
        {
            exact = true;
        }

        if (f == DurationFormat.Short)
        {
            rg = rgs;
        }

        return Length < 100 ? string.Format(rg[0], Length) : exact ? string.Format(rg[1], Minutes) : string.Format(rg[2], Minutes, Seconds);
    }

    #endregion

    #region Standard Durations

    public static IEnumerable<SongDuration> GetStandarDurations()
    {
        return s_durations;
    }

    private static readonly SongDuration[] s_durations =
    [
        new(30M), new(60M), new(90M),
        new(120M), new(150M), new(180M),
        new(240M), new(300M)
    ];

    #endregion
}
