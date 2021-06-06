using System;
using System.Collections.Generic;
using System.Linq;
using m4d.ViewModels;

namespace m4d.Utilities
{
    public static class SpiderManager
    {
        public static DateTime StartTime { get; } = DateTime.Now;
        public static TimeSpan UpTime => DateTime.Now - StartTime;

        public static bool CheckAnySpiders(string userAgent)
        {
            return CheckSpiders(userAgent, false);
        }

        public static bool CheckBadSpiders(string userAgent)
        {
            return CheckSpiders(userAgent, true);
        }

        private static bool CheckSpiders(string userAgent, bool badOnly)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                EmptyAgents += 1;
                return true;
            }

            var agent = userAgent.ToLower();

            if (!agent.Contains("spider") && !agent.Contains("bot")) return false;

            lock (s_botHits)
            {
                if (!s_botHits.TryGetValue(agent, out var count)) count = 0;
                s_botHits[agent] = count + 1;
            }

            return !badOnly || s_badBots.Any(s => agent.Contains(s));
        }

        public static IEnumerable<BotHitModel> CreateBotReport()
        {
            lock (s_botHits)
            {
                return BotHitModel.Create(UpTime, EmptyAgents, s_botHits);
            }
        }

        private static readonly Dictionary<string, long> s_botHits = new();

        public static int EmptyAgents { get; set; }

        private static readonly HashSet<string> s_badBots = new() {"baiduspider"};
    }
}