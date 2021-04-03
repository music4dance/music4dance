using m4dModels;

namespace m4d.ViewModels
{
    public class SongDetailsModel
    {
        public SongHistory SongHistory { get; set; }
        public SongFilterSparse Filter { get; set; }
        public string UserName { get; set; }

        // TODO: Deprecate this once we're confident that we can build a song from history
        public SongSparse Song { get; set; }

    }
}
