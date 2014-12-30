namespace m4dModels
{
    class AmazonService : MusicService
    {
        public AmazonService(ServiceType id, char cid, string name, string target, string description, string link, string request) :
            base(id, cid, name, target, description, link, request)
        {
        }
        protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            album = Strip(album);
            song = Strip(song);

            return base.BuildPurchaseLink(pt, album, song);
        }
        static string Strip(string info)
        {
            if (info != null && (info.StartsWith("A:") || info.StartsWith("D:")))
            {
                info = info.Substring(2);
            }

            return info;
        }

    }
}
