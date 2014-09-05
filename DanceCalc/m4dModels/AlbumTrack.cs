using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            StringBuilder sb = new StringBuilder(title);
            if (track != null)
            {
                sb.AppendFormat("|{0}", track.ToString());
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

        static readonly Regex _validator = new Regex(@"^([\d]{1,3})(:[\d]{1,3}){0,2}$");
        private string[] Split()
        {
            string[] ret = new string[] {null,null};

            if (_val != null)
            {
                int idx = _val.LastIndexOf('|');
                if (idx == -1 || !_validator.IsMatch(_val.Substring(idx + 1)))
                {
                    if (idx==_val.Length-1)
                    {
                        _val = _val.Substring(0,idx);
                    }
                    ret[0] = _val;
                }
                else
                {
                    ret[0] = _val.Substring(0, idx);
                    ret[1] = _val.Substring(idx + 1);
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
            if (other is AlbumTrack)
            {
                return _val.CompareTo(((AlbumTrack)other)._val);
            }
            else
            {
                return -1;
            }
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

        private string _val;
    }
}
