using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace m4d.Models
{
    public interface ISongPropertyFactory
    {
        SongProperty CreateSongProperty(Song song, string name, object value);
    }
}