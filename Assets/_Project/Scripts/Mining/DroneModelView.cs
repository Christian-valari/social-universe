using UnityEngine;

namespace SocialUniverse.Mining
{
    // Lives on the root of every drone model prefab. Each model owns its traveling/mining particle
    // effects; this exposes them so DroneController can drive them after swapping the model in at
    // runtime (the active drone's model varies, so the rig can't hold fixed VFX references).
    public class DroneModelView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _travelingEffect; // played while flying to the asteroid
        [SerializeField] private ParticleSystem _miningEffect;    // played while orbiting/mining

        public ParticleSystem TravelingEffect => _travelingEffect;
        public ParticleSystem MiningEffect     => _miningEffect;
    }
}
