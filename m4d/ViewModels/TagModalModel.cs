using m4dModels;

namespace m4d.ViewModels
{
    public class TagModalModel
    {
        public TagCount Tag { get; set; }
        public SongFilter Filter { get; set; }
        public Song Song { get; set; }
        public float CloudSize { get; set; } = 0.0f;
    }
}
