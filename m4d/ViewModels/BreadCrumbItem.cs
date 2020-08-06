using System.Collections.Generic;

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
            return new List<BreadCrumbItem>
            {
                HomeItem,
                InfoItem,
                new BreadCrumbItem { Title = title, Active = true}
            };
        }

        public static BreadCrumbItem AdminItem =>
            new BreadCrumbItem { Title = "Administration", Link = "/admin" };

        public static List<BreadCrumbItem> BuildAdminTrail(string title)
        {
            return new List<BreadCrumbItem>
            {
                HomeItem,
                AdminItem,
                new BreadCrumbItem { Title = title, Active = true}
            };
        }

        public static BreadCrumbItem ContributeItem =>
            new BreadCrumbItem {Title = "Contribute", Link = "/Home/Contribute"};

        public static List<BreadCrumbItem> BuildContributeTrail(string title)
        {
            return new List<BreadCrumbItem>
            {
                HomeItem,
                InfoItem,
                ContributeItem,
                new BreadCrumbItem { Title = title, Active = true}
            };
        }
    }
}

