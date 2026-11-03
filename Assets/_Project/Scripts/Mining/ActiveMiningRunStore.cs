using System;
using UnityEngine;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // The local marker for the active run in progress, so closing the app on a bad run isn't a free
    // retry. PlayerPrefs is shared by every account on the device, so each marker names its player
    // and Load drops anyone else's. This keeps normal players honest; it isn't a security measure,
    // since the server still clamps every claim.
    public class ActiveMiningRunStore
    {
        private readonly IAuthService _auth;

        public ActiveMiningRunStore(IAuthService auth) => _auth = auth;

        public void Save(string planetId, string slotId, string mineralId, int fullQuantity, float bankedFraction)
        {
            var record = new ActiveMiningRunRecord
            {
                PlayerId       = _auth.PlayerId,
                PlanetId       = planetId,
                SlotId         = slotId,
                MineralId      = mineralId,
                FullQuantity   = fullQuantity,
                BankedFraction = bankedFraction,
            };
            PlayerPrefs.SetString(SaveKeys.ActiveMiningRun, JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }

        // The current player's marker, or null. A malformed marker or another player's is deleted.
        public ActiveMiningRunRecord Load()
        {
            string json = PlayerPrefs.GetString(SaveKeys.ActiveMiningRun, "");
            if (string.IsNullOrEmpty(json)) return null;

            var record = Parse(json);
            if (record != null && record.PlayerId == _auth.PlayerId) return record;

            Clear();
            return null;
        }

        // For a server answer that may have taken a while: by then the marker can be the next run's.
        public void Clear(string slotId)
        {
            if (Load()?.SlotId == slotId) Clear();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);
            PlayerPrefs.Save();
        }

        private static ActiveMiningRunRecord Parse(string json)
        {
            try
            {
                return JsonUtility.FromJson<ActiveMiningRunRecord>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
