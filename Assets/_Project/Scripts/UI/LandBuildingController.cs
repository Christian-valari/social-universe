using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Renders a plot's placed decorations from the LandBuildingHandoff and wires the Back button.
    // Edit-mode palette/slot interaction is added by LandBuildPaletteView (registered in the same
    // scene scope). Lives in the UI assembly because it reads Config/Core and must not create a
    // World->Economy cycle.
    //
    // Plain MonoBehaviour (not IStartable): registered only via RegisterComponentInHierarchy in
    // LandBuildingSceneScope, which injects [Inject] fields at container build time — before
    // Unity's own Start() message runs. This avoids the RegisterEntryPoint overload ambiguity
    // some VContainer versions hit with component-based entry points.
    public class LandBuildingController : MonoBehaviour
    {
        [SerializeField] private Transform[] _slotAnchors;
        [SerializeField] private Button      _backButton;

        [Inject] private LandBuildingHandoff _handoff;
        [Inject] private DatabaseRegistry    _registry;
        [Inject] private IObjectResolver     _resolver;

        private readonly List<GameObject> _spawned = new();

        private void Start()
        {
            _backButton.onClick.AddListener(OnBack);
            Render();
        }

        public void Render()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();

            var slots = _handoff.Slots;
            if (slots == null) return;

            int count = Mathf.Min(slots.Length, _slotAnchors.Length);
            for (int i = 0; i < count; i++)
            {
                var item = LandSlotResolver.Resolve(slots[i], _registry);
                if (item == null || item.Prefab == null) continue;
                var go = Instantiate(item.Prefab, _slotAnchors[i].position, _slotAnchors[i].rotation, _slotAnchors[i]);
                _spawned.Add(go);
            }
        }

        // Public so LandBuildPaletteView can refresh a single anchor after an edit.
        public void SetSlotVisual(int slotIndex, ItemDefinition item)
        {
            if (slotIndex < 0 || slotIndex >= _slotAnchors.Length) return;
            // clear existing child under this anchor
            var anchor = _slotAnchors[slotIndex];
            for (int c = anchor.childCount - 1; c >= 0; c--) Destroy(anchor.GetChild(c).gameObject);
            if (item != null && item.Prefab != null)
                Instantiate(item.Prefab, anchor.position, anchor.rotation, anchor);
        }

        public Transform GetAnchor(int slotIndex) =>
            (slotIndex >= 0 && slotIndex < _slotAnchors.Length) ? _slotAnchors[slotIndex] : null;

        private void OnBack()
        {
            var state = _resolver.Resolve<LandBuildingState>();
            state.Finish();
        }
    }
}
