using System;
using System.Collections.Generic;
using System.Linq;

namespace m4dModels
{
    public class SongMergeModel
    {
        public SongMergeModel(IEnumerable<SongHistory> songs)
        {
            SongId = Guid.NewGuid();
            Songs = songs.ToList();
        }

        public Guid SongId { get; set; }
        public List<SongHistory> Songs { get; set; }
    }
}
