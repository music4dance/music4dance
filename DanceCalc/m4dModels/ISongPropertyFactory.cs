
namespace m4dModels
{
    public interface ISongPropertyFactory
    {
        SongProperty CreateSongProperty(Song song, string name, object value);
    }
}