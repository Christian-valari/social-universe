using UnityEngine;
using VContainer.Unity;

namespace SocialUniverse.Mining
{
    // Advances the active-mining minigame's tap-window timer every frame, so a target point
    // that's never tapped still counts as a miss once its window expires. Active mining has
    // no travel/arrival phase, so unlike IdleMiningSessionController this only drives Tick.
    public class ActiveMiningSessionController : ITickable
    {
        private readonly MiningController _mining;

        public ActiveMiningSessionController(MiningController mining) => _mining = mining;

        public void Tick() => _mining.TickActiveSession(Time.deltaTime);
    }
}
