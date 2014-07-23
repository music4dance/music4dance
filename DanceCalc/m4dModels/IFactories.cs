
namespace m4dModels
{
    public interface IFactories
    {
        SongProperty CreateSongProperty(Song song, string name, object value, SongLog log);
        DanceRating CreateDanceRating(Song song, string danceId, int weight);
    }
}