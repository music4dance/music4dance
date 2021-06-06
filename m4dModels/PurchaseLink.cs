using System.Runtime.Serialization;

namespace m4dModels
{
    [DataContract]
    public class PurchaseLink
    {
        [DataMember] public ServiceType ServiceType { get; set; }
        [DataMember] public string Link { get; set; }
        [DataMember] public string Target { get; set; }
        [DataMember] public string Logo { get; set; }
        [DataMember] public string Charm { get; set; }
        [DataMember] public string AltText { get; set; }
        [DataMember] public string SongId { get; set; }
        [DataMember] public string AlbumId { get; set; }
        [DataMember] public string[] AvailableMarkets { get; set; }
    }

    public class PurchaseInfo
    {
        public PurchaseInfo(PurchaseLink link, bool useLogo)
        {
            Id = MusicService.GetService(link.ServiceType).CID;
            Link = link.Link;
            Target = link.Target;
            Image = useLogo ? link.Logo : link.Charm;
        }

        public char Id { get; set; }
        public string Link { get; set; }
        public string Target { get; set; }

        public string Image { get; set; }
    }
}