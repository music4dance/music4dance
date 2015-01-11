using System;
using System.Diagnostics;

namespace m4dModels
{
    public class ModifiedRecord
    {
        public string ApplicationUserId { get; set; }
        public virtual ApplicationUser ApplicationUser { get; set; }

        public Guid SongId { get; set; }
        public virtual Song Song { get; set; }

        // This is both a boolean to indicate that the user owns the track
        //  and a hash for the filename so that in the future hopefully
        //  we can do a quick match on the user's machine
        public int? Owned { get; set; }
        public string UserName 
        {
            get
            {
                if (ApplicationUser != null)
                {
                    return ApplicationUser.UserName;
                }
                else
                {
                    return _userName;
                }
            }
            set
            {
                if (ApplicationUser != null)
                {
                    Trace.WriteLine(string.Format("Illegal Attempt to redefine user {0} --> {1}",ApplicationUser.UserName, value));
                }
                else
                {
                    _userName = value;
                }
            }
        }
        private string _userName;
    }
}