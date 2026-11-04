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
                "ActiveMining run: tier={0} perfect={1} good={2} late={3} rings={4} decoys={5} wrong={6} veins={7} banked={8:0.00} result={9}",
                tierNumber, session.PerfectCount, session.GoodCount, session.LateCount,
                session.RingMisses, session.DecoyTaps, session.WrongTaps, session.VeinFails, session.BankedFraction, session.Stage);
    }
}
#endif
