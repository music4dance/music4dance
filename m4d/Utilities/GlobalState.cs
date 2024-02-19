using m4dModels;

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
    public string Notice { get; set;}
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public MarketingProduct Product { get; set; }
}

public static class GlobalState
{
    public static string UpdateMessage { get; set; }
    public static bool UseTestKeys { get; set; }

    private static MarketingInfo Marketing { get; set; }

    internal static void SetMarketing(IConfigurationSection configurationSection)
    {
        Marketing = configurationSection.Get<MarketingInfo>();
    }

    public static bool UseCaptcha(IConfiguration configuration)
    {
        var captcha = configuration["Configuration:Registration:CaptchaEnabled"];
        return !string.IsNullOrEmpty(captcha) && bool.TryParse(captcha, out var enabled) && enabled;
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
