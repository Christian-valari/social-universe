using UnityEngine;

namespace SocialUniverse.Core
{
    public static class SULog
    {
        [System.Flags]
        public enum Channel
        {
            None       = 0,
            Core       = 1 << 0,
            Economy    = 1 << 1,
            Net        = 1 << 2,
            World      = 1 << 3,
            Mining     = 1 << 4,
            Social     = 1 << 5,
            UI         = 1 << 6,
            All        = ~0
        }

        public static Channel EnabledChannels = Channel.All;

        public static void Info(string msg, Channel channel = Channel.Core)
        {
            if ((EnabledChannels & channel) != 0)
                Debug.Log($"[SU:{channel}] {msg}");
        }

        public static void Warn(string msg, Channel channel = Channel.Core)
        {
            if ((EnabledChannels & channel) != 0)
                Debug.LogWarning($"[SU:{channel}] {msg}");
        }

        public static void Error(string msg, Channel channel = Channel.Core)
        {
            if ((EnabledChannels & channel) != 0)
                Debug.LogError($"[SU:{channel}] {msg}");
        }
    }
}
