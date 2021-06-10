using m4dModels;

namespace m4d.ViewModels
{
    public class SongDetailsModel
    {
        public bool Created { get; set; }
        public SongHistory SongHistory { get; set; }

        public SongFilterSparse Filter { get; set; }

        // TODO: Should be able to get rid of this in favor of general menucontext
        public string UserName { get; set; }

        // TODO: Deprecate this once we're confident that we can build a song from history
        public SongSparse Song { get; set; }
    }
}
