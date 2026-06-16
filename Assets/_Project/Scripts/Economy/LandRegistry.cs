using System.Collections.Generic;

namespace SocialUniverse.Economy
{
    // Client-side cache of tiles owned by the local player.
    // Hydrated from Cloud Save on scene start; updated after each successful purchase.
    // Source of truth is always the server — never trust this without a prior hydration call.
    public class LandRegistry
    {
        private readonly HashSet<string> _owned = new();

        public void Hydrate(IEnumerable<string> tileIds)
        {
            _owned.Clear();
            foreach (var id in tileIds) _owned.Add(id);
        }

        public void MarkOwned(string tileId) => _owned.Add(tileId);

        public void RemoveOwned(string tileId) => _owned.Remove(tileId);

        public bool IsOwned(string tileId) => _owned.Contains(tileId);

        public IReadOnlyCollection<string> OwnedTileIds => _owned;
    }
}
