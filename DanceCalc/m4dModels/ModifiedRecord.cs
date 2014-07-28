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