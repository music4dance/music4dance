using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using m4dModels;

namespace m4d.ViewModels
{
    // Exact = Title, Artist, Album
    // Length = Title, Artist, Length
    // Weak = Database doesn't already have a 'real' album and tempo so use the new one
    public enum MatchType { None, Weak, Length, Exact};

    public class LocalMerger
    {
        public SongDetails Left { get; set; }
        public SongDetails Right { get; set; }
        public bool Conflict { get; set; }
        public MatchType MatchType { get; set; }
    }
}