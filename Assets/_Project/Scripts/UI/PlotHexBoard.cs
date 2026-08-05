using System.Collections.Generic;
using UnityEngine;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Economy;

namespace SocialUniverse.UI
{
    public static class HexCellVisual
    {
        public enum State { Locked, Empty, Occupied }
        public static State Resolve(bool unlocked, string itemId)
        {
            if (!unlocked) return State.Locked;
            return string.IsNullOrEmpty(itemId) ? State.Empty : State.Occupied;
        }
    }

    // Renders the hex board: one cell GameObject per hexatile (from HexBoardMath layout),
    // each with a collider (raycast target for input) and a building anchor. Locked cells show
    // the lock visual; occupied cells instantiate the item prefab. Lives in the UI assembly
    // because it reads Config/Core/Economy and must not create a World->Economy cycle.
    public class PlotHexBoard : MonoBehaviour
    {
        [SerializeField] private GameObject _cellPrefab;   // a HexCell: mesh + collider + Anchor + Lock + Renderer
        [SerializeField] private float      _cellSize = 0.6f;
        [SerializeField] private Material   _lockedMat;
        [SerializeField] private Material   _unlockedMat;

        [Inject] private DatabaseRegistry _registry;
        [Inject] private EconomyConfig    _config;

        private readonly List<HexCell> _cells = new();

        public int CellCount => _cells.Count;

        public Vector3 CellWorldPosition(int index) =>
            (index >= 0 && index < _cells.Count) ? _cells[index].transform.position : Vector3.zero;

        public void Build(bool[] unlocked, string[] slots)
        {
            foreach (var c in _cells) if (c != null) Destroy(c.gameObject);
            _cells.Clear();

            var positions = HexBoardMath.LocalPositions(_config.HexBoardRadius, _cellSize);
            for (int i = 0; i < positions.Length; i++)
            {
                var go = Instantiate(_cellPrefab, transform);
                go.transform.localPosition = positions[i];
                var cell = go.GetComponent<HexCell>();
                if (cell == null) cell = go.AddComponent<HexCell>();
                cell.Index = i;
                _cells.Add(cell);

                bool u = unlocked != null && i < unlocked.Length && unlocked[i];
                string item = (slots != null && i < slots.Length) ? slots[i] : null;
                SetCell(i, u, item);
            }
        }

        public void SetCell(int index, bool unlocked, string itemId)
        {
            if (index < 0 || index >= _cells.Count) return;
            var cell  = _cells[index];
            var state = HexCellVisual.Resolve(unlocked, itemId);
            cell.SetLockVisual(state == HexCellVisual.State.Locked, _lockedMat, _unlockedMat);

            for (int c = cell.Anchor.childCount - 1; c >= 0; c--) Destroy(cell.Anchor.GetChild(c).gameObject);
            if (state == HexCellVisual.State.Occupied)
            {
                var item = _registry.GetItem(itemId);
                if (item != null && item.Prefab != null)
                    Instantiate(item.Prefab, cell.Anchor.position, cell.Anchor.rotation, cell.Anchor);
            }
        }
    }
}
