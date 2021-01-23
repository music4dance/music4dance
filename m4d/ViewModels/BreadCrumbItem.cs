using System.Collections.Generic;
using System.Linq;

namespace m4d.ViewModels
{
    public class BreadCrumbItem
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public bool? Active { get; set; }

        public static BreadCrumbItem HomeItem =>
            new BreadCrumbItem {Title = "music4dance", Link = "/"};

        public static BreadCrumbItem InfoItem =>
            new BreadCrumbItem {Title = "Info", Link = "/home/info"};

        public static List<BreadCrumbItem> BuildInfoTrail(string title)
        {
            return BuildTrail(title, HomeItem, InfoItem);
        }

        public static BreadCrumbItem AdminItem =>
            new BreadCrumbItem { Title = "Administration", Link = "/admin" };

        public static List<BreadCrumbItem> BuildAdminTrail(string title)
        {
            return BuildTrail(title, HomeItem, AdminItem);
        }

        public static BreadCrumbItem DanceItem =>
            new BreadCrumbItem { Title = "Dances", Link = "/dances" };

        public static List<BreadCrumbItem> BuildDanceTrail(string title)
        {
            return BuildTrail(title, HomeItem, DanceItem);
        }
        public static BreadCrumbItem ContributeItem =>
            new BreadCrumbItem {Title = "Contribute", Link = "/Home/Contribute"};

        public static List<BreadCrumbItem> BuildContributeTrail(string title)
        {
            return BuildTrail(title, HomeItem, InfoItem, ContributeItem);
        }

        public static BreadCrumbItem UsersItem =>
            new BreadCrumbItem { Title = "Users", Link = "/ApplicationUsers" };

        public static List<BreadCrumbItem> BuildUsersTrail(string title)
        {
            return BuildTrail(title, HomeItem, AdminItem, UsersItem);
        }

        public static BreadCrumbItem PlaylistItem =>
            new BreadCrumbItem { Title = "Users", Link = "/Playlist" };

        public static List<BreadCrumbItem> BuildPlaylistTrail(string title)
        {
            return BuildTrail(title, HomeItem, AdminItem, PlaylistItem);
        }

        private static List<BreadCrumbItem> BuildTrail(string title, params BreadCrumbItem[] items)
        {
            return items.Append(new BreadCrumbItem { Title = title, Active = true}).ToList();
        }
    }
}

