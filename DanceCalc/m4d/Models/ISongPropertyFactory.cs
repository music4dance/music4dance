
namespace m4d.Models
{
    public interface ISongPropertyFactory
    {
        SongProperty CreateSongProperty(Song song, string name, object value);
    }
}