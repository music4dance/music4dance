using System;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable RedundantCast.0
// ReSharper disable RedundantCast

namespace m4dModels
{
    // AlbumTrack is a means of moving back and forth between a string encoded
    //  Ablum Name and Track number and a Album Title + Work/Volume/Track #
    public class AlbumTrack : IComparable
    {
        public AlbumTrack(string encoded)
        {
            _val = encoded;
        }

        public AlbumTrack(string title, TrackNumber track)
        {
            var sb = new StringBuilder(title);
            if (track != null)
            {
                sb.AppendFormat("|{0}", track);
            }

            _val = sb.ToString();
        }

        public string Album => Split()[0];

        private static readonly Regex Validaor = new(@"^([\d]{1,3})(:[\d]{1,3}){0,2}$");

        private string[] Split()
        {
            var ret = new string[] { null, null };

            if (_val == null)
            {
                return ret;
            }

            var val = _val;
            var idx = val.LastIndexOf('|');
            if (idx == -1 || !Validaor.IsMatch(_val[(idx + 1)..]))
            {
                if (idx == val.Length - 1)
                {
                    val = val[..idx];
                }

                ret[0] = val;
            }
            else
            {
                ret[0] = val[..idx];
                ret[1] = val[(idx + 1)..];
            }

            return ret;
        }

        public TrackNumber Track => new(Split()[1]);

        #region Operators

        public static implicit operator string(AlbumTrack at)
        {
            return at._val;
        }

        public static implicit operator AlbumTrack(string val)
        {
            return new AlbumTrack(val);
        }

        public int CompareTo(object other)
        {
            var track = other as AlbumTrack;
            return track != null ? string.Compare(_val, track._val, StringComparison.OrdinalIgnoreCase) : -1;
        }

        public override bool Equals(object obj)
        {
            return CompareTo(obj) == 0;
        }

        public override int GetHashCode()
        {
            return _val.GetHashCode();
        }

        public static bool operator ==(AlbumTrack a, AlbumTrack b)
        {
            return a is null || b is null ? a == (object)b : a.Equals(b);
        }

        public static bool operator !=(AlbumTrack a, AlbumTrack b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return _val;
        }

        #endregion

        private readonly string _val;
    }
}
