namespace m4d.ViewModels;

/// <summary>
/// Safe-to-serialise projection of UserInfo for the admin users index page.
/// Deliberately excludes sensitive identity fields (PasswordHash, SecurityStamp, etc.).
/// </summary>
public class AdminUserSummary
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsPseudo { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime LastActive { get; set; }
    public int HitCount { get; set; }
    public decimal LifetimePurchased { get; set; }
    public int SubscriptionLevel { get; set; }
    public byte Privacy { get; set; }
    public int CanContact { get; set; }
    public string ServicePreference { get; set; }
    public int FailedCardAttempts { get; set; }
    public List<string> Roles { get; set; }
    public List<string> Logins { get; set; }
}

/// <summary>
/// Music service identity for the admin users service-preference summary table.
/// </summary>
public class AdminServiceInfo
{
    public string Cid { get; set; }     // single character used in ServicePreference strings
    public string Name { get; set; }
}

/// <summary>
/// Top-level model for the admin-users Vue page.
/// </summary>
public class AdminUsersModel
{
    public List<AdminUserSummary> Users { get; set; }
    public List<string> AllRoles { get; set; }
    public List<AdminServiceInfo> Services { get; set; }
}
