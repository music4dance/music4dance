using System;
using System.Diagnostics;


namespace m4dModels
{

    // Data Format:
    //   Name<US>Value[<US>Old][<RS>Name<US>Value[<US>Old]]*
    public class SongLog : LogBase 
    {
        public int Id { get; set; }
        public virtual ApplicationUser User { get; set; }
        public DateTime Time { get; set; }
        public string Action { get; set; }
        public Guid SongReference { get; set; }
        public string SongSignature { get; set; }

        public void Initialize(ApplicationUser user, Song song, string action)
        {
            Time = DateTime.Now;
            User = user;
            SongReference = song.SongId;
            Action = action;

            SongSignature = song.Signature;
        }

        public bool Initialize(string entry, DanceMusicService dms)
        {
            string[] cells = entry.Split(new char[] { LogBase.RecordSeparator });

            // user<RS>time<RS>command<RS>id<RS>sig<RS>[data]*

            if (cells.Length < 4)
            {
                Trace.WriteLine(string.Format("Bad Line: {0}", entry));
                return false;
            }

            string userName = cells[0];
            string timeString = cells[1];
            Action = cells[2];
            string songRef = cells[3];
            SongSignature = cells[4];

            User = dms.FindUser(userName);
            if (User == null)
            {
                Trace.WriteLine(string.Format("Bad User Name: {0}", userName));
                return false;
            }

            DateTime time;
            if (!DateTime.TryParse(timeString, out time))
            {
                Trace.WriteLine(string.Format("Bad Timestamp: {0}", timeString));
                return false;
            }
            else 
            {
                Time = time;
            }

            Guid songId = Guid.Empty;
            if (!Guid.TryParse(songRef, out songId))
            {
                Trace.WriteLine(string.Format("Bad SongId: {0}", songRef));
                return false;
            }
            else
            {
                SongReference = songId;
            }

            Data = string.Join(new string(RecordSeparator,1), cells, 5, cells.Length - 5);

            return true;
        }
    }
}