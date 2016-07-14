using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace m4dModels
{
    public class ModifiedRecord
    {
        public ModifiedRecord()
        {
        }

        public ModifiedRecord(ModifiedRecord mod)
        {
            // KILLDB: Do we need ApplicationUser & ApplicationUserId in this record?
            // TODO: This is causing Application users to be loaded as 
            //  inidividual TSQL queiries, should we change the top level
            //  song query to load those records or figure out a way
            //  to defer or batch this query?
            ApplicationUserId = mod.ApplicationUserId;
            if (mod.ApplicationUser != null)
            {
                _userName = mod.ApplicationUser.UserName;
            }
            Like = mod.Like;
            Owned = mod.Owned;
        }

        public string ApplicationUserId { get; set; }
        public virtual ApplicationUser ApplicationUser { get; set; }

        // This is both a boolean to indicate that the user owns the track
        //  and a hash for the filename so that in the future hopefully
        //  we can do a quick match on the user's machine
        public int? Owned { get; set; }

        public bool? Like { get; set; }

        public string UserName 
        {
            get {
                return ApplicationUser != null ? ApplicationUser.UserName : _userName;
            }
            set
            {
                if (ApplicationUser != null)
                {
                    Trace.WriteLine($"Illegal Attempt to redefine user {ApplicationUser.UserName} --> {value}");
                }
                else
                {
                    _userName = value;
                }
            }
        }
        private string _userName;

        [NotMapped]
        public string LikeString {
            get {return Like?.ToString() ?? "null";}
            set { ParseLike(value); }
        }

        public static bool? ParseLike(string likeString)
        {
            bool like;
            if (bool.TryParse(likeString, out like))
                return like;
            return null;
        }
    }
}