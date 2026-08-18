using System.Collections.Generic;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Published after any inventory mutation so the Mineral inventory UI can refresh.
    public class MineralInventoryChangedEvent { }

    // Client-side view cache of { mineralId -> qty }. The server (Cloud Save
    // mineral_inventory record) is the source of truth; this mirrors Wallet <-> IEconomyService.
    public class MineralInventory
    {
        private readonly Dictionary<string, int> _held = new();

        public IReadOnlyDictionary<string, int> All => _held;

        public int Get(string mineralId) =>
            mineralId != null && _held.TryGetValue(mineralId, out var q) ? q : 0;

        public void SetAll(IReadOnlyDictionary<string, int> source)
        {
            _held.Clear();
            if (source != null)
                foreach (var kv in source)
                    if (kv.Value > 0) _held[kv.Key] = kv.Value;
            EventBus.Publish(new MineralInventoryChangedEvent());
        }

        public void Add(string mineralId, int qty)
        {
            if (string.IsNullOrEmpty(mineralId) || qty == 0) return;
            _held.TryGetValue(mineralId, out var current);
            int next = current + qty;
            if (next <= 0) _held.Remove(mineralId);
            else            _held[mineralId] = next;
            EventBus.Publish(new MineralInventoryChangedEvent());
        }

        public int TotalSellValue(DatabaseRegistry registry)
        {
            int total = 0;
            foreach (var kv in _held)
            {
                var def = registry.GetMineral(kv.Key);
                if (def != null) total += kv.Value * def.SellValue;
            }
            return total;
        }
    }
}
