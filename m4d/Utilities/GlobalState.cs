using Microsoft.Extensions.Configuration;
using System;

namespace m4d.Utilities
{
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
        public bool Enabled { get; set; }
        public string Banner { get; set; }
        public string Notice { get; set;}
        public MarketingProduct Product { get; set; }
    }

    public static class GlobalState
    {
        public static string UpdateMessage { get; set; }
        public static bool UseTestKeys { get; set; }

        public static MarketingInfo Marketing { get; set; }

        internal static void SetMarketing(IConfigurationSection configurationSection)
        {
            Marketing = configurationSection.Get<MarketingInfo>();
        }
    }
}
