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
            var cells = entry.Split(RecordSeparator);

            // user<RS>time<RS>command<RS>id<RS>sig<RS>[data]*

            if (cells.Length < 4)
            {
                Trace.WriteLine($"Bad Line: {entry}");
                return false;
            }

            var userName = cells[0];
            var timeString = cells[1];
            Action = cells[2];
            var songRef = cells[3];
            SongSignature = cells[4];

            User = dms.FindUser(userName);
            if (User == null)
            {
                Trace.WriteLine($"Bad User Name: {userName}");
                return false;
            }

            DateTime time;
            if (!DateTime.TryParse(timeString, out time))
            {
                Trace.WriteLine($"Bad Timestamp: {timeString}");
                return false;
            }
            Time = time;

            Guid songId;
            if (!Guid.TryParse(songRef, out songId))
            {
                Trace.WriteLine($"Bad SongId: {songRef}");
                return false;
            }
            SongReference = songId;

            Data = string.Join(RecordString, cells, 5, cells.Length - 5);

            return true;
        }
    }
}