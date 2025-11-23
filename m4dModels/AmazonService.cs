namespace m4dModels;

internal class AmazonService : MusicService
{
    public AmazonService() :
        base(
            ServiceType.Amazon,
            'A',
            "Amazon",
            "amazon_store",
            "Available on Amazon",
            "http://www.amazon.com/gp/product/{0}/ref=as_li_ss_tl?ie=UTF8&camp=1789&creative=390957&creativeASIN={0}&linkCode=as2&tag=msc4dnc-20",
            null)
    {
    }

    public override bool IsSearchable => false;

    protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
    {
        album = Strip(album);
        song = Strip(song);

        return base.BuildPurchaseLink(pt, album, song);
    }

    public override string NormalizeId(string id)
    {
        if (!id.Contains(':'))
        {
            id = "D:" + id;
        }

        return id;
    }

    private static string Strip(string info)
    {
        if (info != null && (info.StartsWith("A:") || info.StartsWith("D:")))
        {
            info = info[2..];
        }

        return info;
    }
}
