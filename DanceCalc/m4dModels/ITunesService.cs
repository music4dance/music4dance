using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class ITunesService : MusicService
    {
        public ITunesService(ServiceType id, char cid, string name, string target, string description, string link) :
            base(id, cid, name, target, description, link)
        {
        }
        protected override string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            // TODO: itunes would need a different kind of link for album only lookup...
            if (pt == PurchaseType.Song && album != null && song != null)
            {
                return string.Format(_associateLink, song, album);
            }
            else
            {
                return null;
            }
        }
    }

}
