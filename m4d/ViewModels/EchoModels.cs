using m4dModels;

namespace m4d.ViewModels
{
    public class EchoHeaderModel
    {
        public string Type { get; set; }
        public bool CanSort { get; set; }
        public SongSort SongOrder { get; set; }
        public SongFilter Filter { get; set; }
    }

    public class EchoValueBase
    {
        public int Size { get; set; } = 25;
        public bool Description { get; set; }
    }

    public class EchoValueModel : EchoValueBase
    {
        public string Type { get; set; }
        public float? Value { get; set; }
    }

    public class EchoValuesModel : EchoValueBase
    {
        public Song Song { get; set; }
    }
}
