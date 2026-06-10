using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Drives active mining via Space bar; commits cargo when full.
    public class MiningInputHandler : ITickable
    {
        private readonly MiningController _controller;

        public MiningInputHandler(MiningController controller) => _controller = controller;

        public void Tick()
        {
            if (_controller.Phase != MiningPhase.Active) return;
            if (!Input.GetKeyDown(KeyCode.Space)) return;

            var result = _controller.Tap();
            if (result == null) return;

            SULog.Info($"Tap: +{result.YieldAmount}{(result.IsCrit ? " CRIT" : "")}", SULog.Channel.Mining);

            if (_controller.Drone.IsCargoFull)
                _ = _controller.CommitCargoAsync();
        }
    }
}
