#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;

namespace SocialUniverse.Mining
{
    // One line per finished active-mining run: the tier it played and how it went, so a playtest on
    // a device can be read back from the log. Formatted apart from the logging so it can be tested.
    public static class ActiveMiningRunLog
    {
        public static string Format(int tierNumber, ActiveMiningSession session) =>
            string.Format(CultureInfo.InvariantCulture,
                "ActiveMining run: tier={0} perfect={1} good={2} late={3} misses={4} banked={5:0.00} result={6}",
                tierNumber, session.PerfectCount, session.GoodCount, session.LateCount,
                session.ErrorCount, session.BankedFraction, session.Stage);
    }
}
#endif
