namespace m4dModels
{
    public class SubscriptionLevelDescription(SubscriptionLevel level, decimal price)
    {
        public SubscriptionLevel Level { get; set; } = level;
        public decimal Price { get; set; } = price;
        public string Name => Level.ToString();

        public static List<SubscriptionLevelDescription> SubscriptionLevels =
        [
            new SubscriptionLevelDescription(SubscriptionLevel.Gold, 100.00M),
            new SubscriptionLevelDescription(SubscriptionLevel.Silver, 50.00M),
            new SubscriptionLevelDescription(SubscriptionLevel.Bronze, 25.00M),
            new SubscriptionLevelDescription(SubscriptionLevel.Basic, 15.00M),
        ];

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
