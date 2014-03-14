using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace m4d.Models
{

    // Data Format:
    //   Name\tValue[\tOld][|Name\tValue[\tOld]]*
    public class SongLog : DbObject
    {
        public int Id { get; set; }
        public virtual ApplicationUser User { get; set; }
        public DateTime Time { get; set; }
        public string Action { get; set; }
        public int SongReference { get; set; }        
        public string SongSignature { get; set; }
        public string Data { get; set; }

        public void Initialize(ApplicationUser user, Song song, string action)
        {
            Time = DateTime.Now;
            User = user;
            SongReference = song.SongId;
            Action = action;

            SongSignature = song.Signature;
        }

        public bool Initialize(DanceMusicContext dmc, string entry)
        {
            string[] cells = entry.Split(new char[] { '|' });

            // user|time|command|id|sig|data...

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

            User = dmc.FindUser(userName);
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

            int songId = 0;
            if (!int.TryParse(songRef, out songId))
            {
                Trace.WriteLine(string.Format("Bad SongId: {0}", songRef));
                return false;
            }
            else
            {
                SongReference = songId;
            }

            Data = string.Join("|",cells,5,cells.Length-5);

            return true;
        }

        public void UpdateData(string name, string value, string oldValue = null)
        {
            if (string.IsNullOrWhiteSpace(Data))
            {
                Data = string.Empty;
            }
            else
            {
                Data += "|";
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = string.Empty;
            }

            value = value.Replace('|', '_');

            if (oldValue != null)
            {
                oldValue = oldValue.Replace('|', '_');
            }

            Data += string.Format("{0}\t{1}", name, value);
            if (oldValue != null)
            {
                Data += string.Format("\t{0}", oldValue);
            }
        }

        public int? GetIntData(string name)
        {
            int? ret = null;

            string s = GetData(name);

            if (s != null)
            {
                int r;
                if (int.TryParse(s, out r))
                {
                    ret = r;
                }
            }

            return ret;
        }

        public string GetData(string name)
        {
            LogValue lv = FindCell(name);

            if (lv != null)
            {
                return lv.Value;
            }
            else
            {
                return null;
            }
        }
        public string GetOld(string name)
        {
            LogValue lv = FindCell(name);

            if (lv != null)
            {
                return lv.Old;
            }
            else
            {
                return null;
            }
        }

        public LogValue FindCell(string name)
        {
            IList<LogValue> values = GetValues();

            if (values != null)
            {
                foreach (LogValue lv in values)
                {

                    if (lv.Name.Equals(name))
                    {
                        return lv;
                    }
                }
            }

            return null;
        }

        public IList<LogValue> GetValues()
        {
            List<LogValue> values = null;
            if (!string.IsNullOrWhiteSpace(Data))
            {
                values = new List<LogValue>();

                string[] entries = Data.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string entry in entries)
                {
                    string name = null;
                    string value = null;
                    string old = null;

                    string[] cells = entry.Split(new char[] { '\t' });

                    if (cells.Length > 0)
                    {
                        name = cells[0];

                        if (cells.Length > 1)
                        {
                            value = cells[1];
                            if (cells.Length > 2)
                            {
                                old = cells[2];
                            }
                        }
                    }

                    values.Add(new LogValue { Name = name, Value = value, Old = old });
                }
            }

            return values;
        }
        
        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},User={1},Time={2},Action={3},Song={4}", Id, User.UserName, Time, Action, SongReference);
            Trace.WriteLine(output);
            Trace.WriteLine(Data);
        }

    }

    public class LogValue
    {
        public LogValue()
        {

        }

        public LogValue(string name, string value, string old = null)
        {
            Name = name;
            Value = value;
            Old = old;
        }

        public bool IsAction
        {
            get { return SongProperty.IsActionName(Name); }
        }

        public bool IsComplex
        {
            get { return SongProperty.IsComplexName(Name);}
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public string Old { get; set; }
    }
}