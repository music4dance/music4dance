using System.Runtime.Serialization;

namespace m4dModels
{
    [DataContract]
    public class ServiceTrack
    {
        [DataMember]
        public ServiceType Service { get; set; }
        [DataMember]
        public string TrackId { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string CollectionId { get; set; }
        [DataMember]
        public string AltId { get; set; }
        [DataMember]
        public string Artist { get; set; }
        [DataMember]
        public string Album { get; set; }
        [DataMember]
        public string ImageUrl { get; set; }
        [DataMember]
        public PurchaseLink SongLink { get; set; }
        [DataMember]
        public PurchaseLink AlbumLink { get; set; }
        [DataMember]
        public string PurchaseInfo { get; set; }
        [DataMember]
        public string ReleaseDate { get; set; }
        [DataMember]
        public string Genre { get; set; }
        [DataMember]
        public int? Duration { get; set; }
        [DataMember]
        public int? TrackNumber { get; set; }
        [DataMember]
        public int? TrackRank { get; set; }
    }
}
