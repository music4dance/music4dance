namespace m4d.ViewModels;

public class BreadCrumbItem
{
    public string Title { get; set; }
    public string Link { get; set; }
    public bool? Active { get; set; }

    public static BreadCrumbItem HomeItem =>
        new() { Title = "music4dance", Link = "/" };

    public static BreadCrumbItem InfoItem =>
        new() { Title = "Info", Link = "/home/info" };

    public static BreadCrumbItem SongLibraryItem =>
        new() { Title = "Song Library", Link = "/home/song" };

    public static BreadCrumbItem AdminItem =>
        new() { Title = "Administration", Link = "/admin" };

    public static BreadCrumbItem DanceItem =>
        new() { Title = "Dances", Link = "/dances" };

    public static BreadCrumbItem ContributeItem =>
        new() { Title = "Contribute", Link = "/Home/Contribute" };

    public static BreadCrumbItem UsersItem =>
        new() { Title = "Users", Link = "/ApplicationUsers" };

    public static BreadCrumbItem PlaylistItem =>
        new() { Title = "Users", Link = "/Playlist" };

    public static List<BreadCrumbItem> BuildInfoTrail(string title)
    {
        return BuildTrail(title, HomeItem, InfoItem);
    }

    public static List<BreadCrumbItem> BuildSongLibraryTrail(string title)
    {
        return BuildTrail(title, HomeItem, SongLibraryItem);
    }

    public static List<BreadCrumbItem> BuildAdminTrail(string title)
    {
        return BuildTrail(title, HomeItem, AdminItem);
    }

    public static List<BreadCrumbItem> BuildDanceTrail(string title)
    {
        return BuildTrail(title, HomeItem, DanceItem);
    }

    public static List<BreadCrumbItem> BuildContributeTrail(string title)
    {
        return BuildTrail(title, HomeItem, InfoItem, ContributeItem);
    }

    public static List<BreadCrumbItem> BuildUsersTrail(string title)
    {
        return BuildTrail(title, HomeItem, AdminItem, UsersItem);
    }

    private static List<BreadCrumbItem> BuildTrail(string title, params BreadCrumbItem[] items)
    {
        return items.Append(new BreadCrumbItem { Title = title, Active = true }).ToList();
    }
}
