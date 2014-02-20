using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace SongDatabase.Models
{

    // Data Format:
    //   Name\tValue[\tOld][|Name\tValue[\tOld]]*
    public class SongLog : DbObject
    {
        public int Id { get; set; }
        public virtual UserProfile User { get; set; }
        public DateTime Time { get; set; }
        public string Action { get; set; }
        public int SongReference { get; set; }        
        public string SongSignature { get; set; }
        public string Data { get; set; }

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
            string value;
            string old;

            FindCell(name, out value, out old);

            return value;
        }
        public string GetOld(string name)
        {
            string value;
            string old;

            FindCell(name, out value, out old);

            return old;
        }

        private bool FindCell(string name, out string value, out string old)
        {
            bool success = false;
            value = null;
            old = null;

            if (!string.IsNullOrWhiteSpace(Data))
            {
                string[] entries = Data.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string entry in entries)
                {
                    string[] cells = entry.Split(new char[] { '\t' });

                    if (cells.Length > 0 && cells[0].Equals(name))
                    {
                        if (cells.Length > 1)
                        {
                            value = cells[1];
                            if (cells.Length > 2)
                            {
                                old = cells[2];
                            }
                        }
                    }
                }
            }

            return success;
        }

        public void Init(UserProfile user, Song song, string action)
        {
            Time = DateTime.Now;
            User = user;
            SongReference = song.SongId;
            Action = action;

            SongSignature = song.Signature;
        }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},User={1},Time={2},Action={3},Song={4}", Id, User.UserName, Time, Action, SongReference);
            Debug.WriteLine(output);
            Debug.WriteLine(Data);
        }

    }

}