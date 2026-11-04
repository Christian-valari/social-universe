using System;
using UnityEngine;

namespace SocialUniverse.Config
{
    // One row of the active-mining difficulty table in EconomyConfig. The default
    // constructor yields Tier 1, so an empty or missing table still plays.
    [Serializable]
    public class ActiveMiningTierSettings
    {
        [SerializeField] private int   _targets          = 8;     // rings to hit in a run
        [SerializeField] private float _ringSeconds      = 1.6f;  // how long a ring stays on screen
        [SerializeField] private float _perfectBySeconds = 0.8f;  // a hit at or before this age is Perfect
        [SerializeField] private float _goodBySeconds    = 1.2f;  // a hit at or before this age is Good; later is Late
        [SerializeField] private int   _maxMisses        = 3;     // misses before the asteroid is lost
        [SerializeField] private float _decoyChance      = 0f;    // chance a target spawns with a decoy beside it

        [SerializeField] private int   _liveTargets      = 1;     // targets that can be live at once
        [SerializeField] private float _minSpinDegrees   = 5f;    // slowest the asteroid spins, in degrees per second
        [SerializeField] private float _maxSpinDegrees   = 15f;   // fastest it spins

        public ActiveMiningTierSettings() { }

        public ActiveMiningTierSettings(int targets, float ringSeconds, float perfectBySeconds, float goodBySeconds, int maxMisses, float decoyChance = 0f, int liveTargets = 1, float minSpinDegrees = 5f, float maxSpinDegrees = 15f)
        {
            _liveTargets      = liveTargets;
            _targets          = targets;
            _ringSeconds      = ringSeconds;
            _perfectBySeconds = perfectBySeconds;
            _goodBySeconds    = goodBySeconds;
            _maxMisses        = maxMisses;
            _decoyChance      = decoyChance;
            _minSpinDegrees   = minSpinDegrees;
            _maxSpinDegrees   = maxSpinDegrees;
        }

        public int   Targets          => _targets;
        public float RingSeconds      => _ringSeconds;
        public float PerfectBySeconds => _perfectBySeconds;
        public float GoodBySeconds    => _goodBySeconds;
        public int   MaxMisses        => _maxMisses;
        public float DecoyChance      => _decoyChance;
        public int   LiveTargets      => _liveTargets;
        public float MinSpinDegrees   => _minSpinDegrees;
        public float MaxSpinDegrees   => _maxSpinDegrees;
    }
}
