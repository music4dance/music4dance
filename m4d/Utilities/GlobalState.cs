namespace m4d.Utilities;

// TODO: This only works for single application instances, eventually
//  move this to some kin of data store/azure supported settings manager

public class MarketingProduct
{
    public string Name { get; set; }
    public string Link { get; set; }
    public string Password { get; set; }
}

public class MarketingInfo
{
    public MarketingInfo ForPage(bool showBanner)
    {
        var page = MemberwiseClone() as MarketingInfo;
        page.Banner = showBanner ? Banner : null;
        return page;
    }

    public bool Enabled { get; set; }
    public string Banner { get; set; }
    public string Notice { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public MarketingProduct Product { get; set; }
}

public static class GlobalState
{
    public static string UpdateMessage { get; set; }
    public static bool UseTestKeys { get; set; }
    public static bool RateLimitLogging { get; set; }

    private static DateTime _requireCaptchaUntil = DateTime.MinValue;
    private static readonly object _captchaLock = new object();

    /// <summary>
    /// Gets or sets whether CAPTCHA is required. When set to true, automatically expires after 5 minutes.
    /// Use SetRequireCaptcha() for explicit timeout control.
    /// </summary>
    public static bool RequireCaptcha
    {
        get
        {
            lock (_captchaLock)
            {
                return DateTime.UtcNow < _requireCaptchaUntil;
            }
        }
        set
        {
            if (value)
            {
                SetRequireCaptcha(TimeSpan.FromMinutes(5));
            }
            else
            {
                lock (_captchaLock)
                {
                    _requireCaptchaUntil = DateTime.MinValue;
                }
            }
        }
    }

    /// <summary>
    /// Enables CAPTCHA requirement for a specific duration.
    /// </summary>
    public static void SetRequireCaptcha(TimeSpan duration)
    {
        lock (_captchaLock)
        {
            var newExpiry = DateTime.UtcNow.Add(duration);
            if (newExpiry > _requireCaptchaUntil)
            {
                _requireCaptchaUntil = newExpiry;
            }
        }
    }

    private static MarketingInfo Marketing { get; set; }

    internal static void SetMarketing(IConfigurationSection configurationSection)
    {
        Marketing = configurationSection.Get<MarketingInfo>();
    }

    public static MarketingInfo GetMarketing(ApplicationUser user, string page)
    {
        if (Marketing == null || !Marketing.Enabled)
        {
            return new MarketingInfo();
        }

        if (Marketing.Start.HasValue && Marketing.Start.Value > DateTime.Now ||
            Marketing.End.HasValue && Marketing.End.Value < DateTime.Now)
        {
            return new MarketingInfo();
        }

        var showBanner = true;
        var path = page.ToLowerInvariant();
        if ((user != null && user.SubscriptionLevel >= SubscriptionLevel.Bronze && user.SubscriptionEnd > new DateTime(2024, 11, 30)) ||
            path.Contains("/contribute") || path.Contains("/payment/"))
        {
            showBanner = false;
        }

        return Marketing.ForPage(showBanner);
    }
}
