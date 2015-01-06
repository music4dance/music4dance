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

        public string Album
        {
            get
            {
                return Split()[0];
            }
        }

        static readonly Regex s_validator = new Regex(@"^([\d]{1,3})(:[\d]{1,3}){0,2}$");
        private string[] Split()
        {
            string[] ret = new string[] {null,null};

            if (_val != null)
            {
                var val = _val;
                int idx = val.LastIndexOf('|');
                if (idx == -1 || !s_validator.IsMatch(_val.Substring(idx + 1)))
                {
                    if (idx==val.Length-1)
                    {
                        val = val.Substring(0,idx);
                    }
                    ret[0] = val;
                }
                else
                {
                    ret[0] = val.Substring(0, idx);
                    ret[1] = val.Substring(idx + 1);
                }
            }

            return ret;
        }
        public TrackNumber Track
        {
            get 
            {
                return new TrackNumber(Split()[1]);
            }
        }

        #region Operators
        static public implicit operator string(AlbumTrack at)
        {
            return at._val;
        }

        static public implicit operator AlbumTrack(string val)
        {
            return new AlbumTrack(val);
        }

        public int CompareTo(object other)
        {
            var track = other as AlbumTrack;
            if (track != null)
                return string.Compare(_val,track._val,StringComparison.OrdinalIgnoreCase);
            else
                return -1;
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
            if ((object)a == null || (object)b == null)
            {
                return (object)a == (object)b;
            }
            else
            {
                return a.Equals(b);
            }
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
