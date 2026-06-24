using System;
using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.Config
{
    [Serializable]
    public class TravelTimeEntry
    {
        public string OriginPlanetId;
        public string DestinationPlanetId;
        public float  TravelSeconds;
    }

    // Per-origin/destination travel durations, authored from Data/Travel_Times.csv
    // (real-distance-derived, not just a flat per-destination number — the same
    // destination takes a different amount of time depending on where the trip
    // starts). Pairs not present here (e.g. anything involving Pluto, which the
    // source data doesn't cover) fall back to the target PlanetDefinition's own
    // TravelDurationSeconds — see TravelService.GetTravelDuration.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/TravelTimeTable", fileName = "TravelTimeTable")]
    public class TravelTimeTable : ScriptableObject
    {
        [SerializeField] private TravelTimeEntry[] _entries;

        private Dictionary<(string, string), float> _lookup;

        public bool TryGetSeconds(string originPlanetId, string destinationPlanetId, out float seconds)
        {
            BuildLookupIfNeeded();
            return _lookup.TryGetValue((originPlanetId, destinationPlanetId), out seconds);
        }

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;

            _lookup = new Dictionary<(string, string), float>();
            if (_entries == null) return;

            foreach (var entry in _entries)
                _lookup[(entry.OriginPlanetId, entry.DestinationPlanetId)] = entry.TravelSeconds;
        }
    }
}
