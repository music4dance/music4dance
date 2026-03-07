using System.Collections.Concurrent;

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
    /// <summary>
    /// Application start time (UTC). Used for uptime calculations across various subsystems.
    /// </summary>
    public static DateTime StartTime { get; } = DateTime.UtcNow;

    /// <summary>
    /// Time elapsed since application start.
    /// </summary>
    public static TimeSpan UpTime => DateTime.UtcNow - StartTime;

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

    /// <summary>
    /// Tracks Meta/Facebook crawler requests that were short-circuited by the rate limiting middleware.
    /// These requests never reach the rate-limiting pipeline, so they aren't captured by RateLimitingTracker.
    /// </summary>
    public static class MetaCrawlerStats
    {
        private static long _totalCount;
        private static readonly ConcurrentDictionary<string, long> _byCrawler = new();
        private static long _lastSeenTicks = DateTime.MinValue.Ticks;

        public static long TotalCount => Interlocked.Read(ref _totalCount);
        public static DateTime LastSeen => new DateTime(Interlocked.Read(ref _lastSeenTicks), DateTimeKind.Utc);
        public static IReadOnlyDictionary<string, long> ByCrawler => _byCrawler;

        /// <summary>
        /// Average short-circuited requests per hour since server start.
        /// </summary>
        public static double AveragePerHour
        {
            get
            {
                var total = TotalCount;
                if (total == 0) return 0;
                var hours = (DateTime.UtcNow - GlobalState.StartTime).TotalHours;
                return hours > 0 ? total / hours : total;
            }
        }

        public static void Record(string userAgent)
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Exchange(ref _lastSeenTicks, DateTime.UtcNow.Ticks);

            var crawlerName = CategorizeCrawler(userAgent);
            _byCrawler.AddOrUpdate(crawlerName, 1, (_, count) => count + 1);
        }

        private static string CategorizeCrawler(string ua)
        {
            var lower = ua.ToLowerInvariant();
            if (lower.Contains("facebookexternalhit")) return "facebookexternalhit";
            if (lower.Contains("facebot")) return "Facebot";
            if (lower.Contains("meta-externalfetcher")) return "meta-externalfetcher";
            return "Other Meta";
        }
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
