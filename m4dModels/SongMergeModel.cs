namespace m4dModels
{
    public class SongMergeModel(IEnumerable<SongHistory> songs)
    {
        public Guid SongId { get; set; } = Guid.NewGuid();
        public List<SongHistory> Songs { get; set; } = [.. songs];
    }
}
