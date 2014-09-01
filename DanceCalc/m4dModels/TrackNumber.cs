using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    // TrackNumber is an extended [work][album]track index
    //  all three are 1 based with max value of 999
    //  work and album can be null (zero represnets null)
    // String format is [www:][aaa:][ttt], string contructor can
    //  take full album name?
    public class TrackNumber
    {
        #region Constructors
        public TrackNumber(int num)
        {
            _val = num;
        }

        public TrackNumber(int track, int? album, int? work)
        {
            Initialize(track, album, work);
        }

        public TrackNumber(string s)
        {
            int track = 0;
            int album = 0;
            int work = 0;

            string[] cells = s.Split(new char[] { ':' });
            if (cells.Length > 0 && int.TryParse(cells[cells.Length - 1], out track))
            {
                if (cells.Length > 1 && int.TryParse(cells[cells.Length - 2], out album))
                {
                    if (cells.Length > 2)
                    {
                        int.TryParse(cells[cells.Length - 3], out work);
                    }
                }
            }

            Initialize(track, album, work);
        }

        private void Initialize(int? track, int? album, int? work)
        {
            int t = 0;
            int a = 0;
            int w = 0;

            if (track.HasValue)
            {
                t = track.Value;
            }

            if (album.HasValue)
            {
                a = album.Value;
            }

            if (work.HasValue)
            {
                w = work.Value;
            }

            _val = t + (a * 1000) + (w * 1000000);
        }
        
        #endregion

        #region Properties
        int? Track
        {
            get
            {
                int? track = _val % 1000;
                if (track.Value == 0)
                {
                    track = null;
                }
                return track;
            }
            set
            {
                Initialize(value, Album, Work);
            }
        }

        int? Album
        {
            get
            {
                int? album = (_val / 1000) % 1000;
                if (album.Value == 0)
                {
                    album = null;
                }
                return album;
            }
            set
            {
                Initialize(Track, value, Work);
            }
        }

        int? Work
        {
            get
            {
                int? work = (_val / 1000) % 1000;
                if (work.Value == 0)
                {
                    work = null;
                }
                return work;
            }
            set
            {
                Initialize(Track, value, Work);
            }
        }
        
        #endregion

        #region Operators
        static public implicit operator int(TrackNumber track)
        {
            return track._val;
        }

        static public implicit operator TrackNumber(int val)
        {
            return new TrackNumber(val);
        }

        public int CompareTo(TrackNumber other)
        {
            return _val.CompareTo(other._val);
        }

        public override bool Equals(object obj)
        {
            if (obj is TrackNumber)
            {
                TrackNumber other = (TrackNumber)obj;
                return CompareTo(other) == 0;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return _val.GetHashCode();
        }

        public static bool operator ==(TrackNumber a, TrackNumber b)
        {
            return a._val == b._val;
        }

        public static bool operator !=(TrackNumber a, TrackNumber b)
        {
            return a._val != b._val;
        }
        #endregion

        //TODONEXT: Formatting and unit test
        private int _val;
    }
}
