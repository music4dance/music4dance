using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace SongDatabase.Models
{

    public class SongLog : DbObject
    {
        public int Id { get; set; }
        public virtual UserProfile User { get; set; }
        public DateTime Time { get; set; }
        public string Action { get; set; }
        public int SongReference { get; set; }
        public string Data { get; set; }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},User={1},Time={2},Action={3},Song={4}", Id, User.UserName, Time, Action, SongReference);
            Debug.WriteLine(output);
            Debug.WriteLine(Data);
        }
    }

}