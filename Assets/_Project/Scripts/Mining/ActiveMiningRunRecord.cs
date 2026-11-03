using System;

namespace SocialUniverse.Mining
{
    // An active run as saved on this device: enough to settle it on the next Planet load if the app
    // closes before its claim is answered. FullQuantity is the amount pinned when the run began.
    [Serializable]
    public class ActiveMiningRunRecord
    {
        public string PlayerId;
        public string PlanetId;
        public string SlotId;
        public string MineralId;
        public int    FullQuantity;
        public float  BankedFraction;
    }
}
