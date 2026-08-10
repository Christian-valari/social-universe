using UnityEngine;

namespace SocialUniverse.UI
{
    // Attached to each hexatile cell (prefab instance); identifies the hex index for raycast
    // hits and exposes the building anchor + lock/renderer visuals PlotHexBoard drives.
    public class HexCell : MonoBehaviour
    {
        public int Index;
        [SerializeField] private Transform _anchor;
        [SerializeField] private GameObject _lock;
        [SerializeField] private Renderer   _renderer;

        public Transform Anchor => _anchor != null ? _anchor : transform;

        public void SetLockVisual(bool locked, Material lockedMat, Material unlockedMat)
        {
            // if (_lock != null) _lock.SetActive(locked);
            if (_renderer != null && lockedMat != null && unlockedMat != null)
                _renderer.sharedMaterial = locked ? lockedMat : unlockedMat;
        }
    }
}
