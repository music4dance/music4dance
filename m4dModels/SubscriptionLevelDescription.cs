using m4dModels;
using System.Collections.Generic;
using System.Linq;

namespace m4d.Controllers
{
    public class SubscriptionLevelDescription
    {
        public SubscriptionLevelDescription(SubscriptionLevel level, decimal price)
        {
            Level = level;
            Price = price;
        }
        public SubscriptionLevel Level { get; set; }
        public decimal Price { get; set; }
        public string Name => Level.ToString();

        public static List<SubscriptionLevelDescription> SubscriptionLevels = new()
        {
            new SubscriptionLevelDescription(SubscriptionLevel.Gold, 100.00M),
            new SubscriptionLevelDescription(SubscriptionLevel.Silver, 50.00M),
            new SubscriptionLevelDescription(SubscriptionLevel.Bronze, 25.00M),
            new SubscriptionLevelDescription(SubscriptionLevel.Basic, 15.00M),
        };

        public static SubscriptionLevelDescription FindSubscriptionLevel(decimal amount)
        {
            return SubscriptionLevels.FirstOrDefault(level => level.Price <= amount);
        }
        public static SubscriptionLevelDescription FindSubscriptionLevel(SubscriptionLevel level)
        {
            return SubscriptionLevels.FirstOrDefault(d => d.Level == level);
        }

    }
}
