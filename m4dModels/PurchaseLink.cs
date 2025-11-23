using System.Runtime.Serialization;

namespace m4dModels;

[DataContract]
public class PurchaseLink
{
    [DataMember]
    public ServiceType ServiceType { get; set; }

    [DataMember]
    public string Link { get; set; }

    [DataMember]
    public string Target { get; set; }

    [DataMember]
    public string Logo { get; set; }

    [DataMember]
    public string Charm { get; set; }

    [DataMember]
    public string AltText { get; set; }

    [DataMember]
    public string SongId { get; set; }

    [DataMember]
    public string AlbumId { get; set; }

    [DataMember]
    public string[] AvailableMarkets { get; set; }
}

public class PurchaseInfo(PurchaseLink link, bool useLogo)
{
    public char Id { get; set; } = MusicService.GetService(link.ServiceType).CID;
    public string Link { get; set; } = link.Link;
    public string Target { get; set; } = link.Target;

    public string Image { get; set; } = useLogo ? link.Logo : link.Charm;
}
