using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class XboxService : MusicService
    {
        public XboxService(ServiceType id, char cid, string name, string target, string description, string link) :
            base(id, cid, name, target, description, link)
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
            if (info != null && info.StartsWith("music."))
            {
                info = info.Substring(6);
            }

            return info;
        }
    }
}
