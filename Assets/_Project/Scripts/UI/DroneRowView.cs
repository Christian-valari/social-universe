using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Config;
using TMPro;

namespace SocialUniverse.UI
{
    // View-model for one upgradeable-stat meter on a drone card. DroneGarageView builds these
    // (it owns the domain math); DroneRowView just presents them.
    public readonly struct DroneStatVm
    {
        public readonly DroneStat Stat;
        public readonly int       Level;
        public readonly int       MaxLevel;
        public readonly int       Cost;
        public readonly bool      Maxed;
        public readonly bool      CanAfford;
        public readonly Action    OnUpgrade;

        public DroneStatVm(DroneStat stat, int level, int maxLevel, int cost, bool maxed, bool canAfford, Action onUpgrade)
        {
            Stat = stat; Level = level; MaxLevel = maxLevel; Cost = cost; Maxed = maxed; CanAfford = canAfford; OnUpgrade = onUpgrade;
        }
    }

    // One drone card in the Drone Garage carousel. A single prefab serves BOTH an owned drone
    // (icon + per-stat pip meters with a "+" upgrade button) and an acquirable drone (icon +
    // acquire button); the widgets not relevant to the current bind are hidden. The card owns its
    // child-widget references via the Inspector, so DroneGarageView never reaches in by name.
    public class DroneRowView : MonoBehaviour
    {
        // One upgrade meter on the card, tagged with the stat it drives.
        [Serializable]
        public class StatMeter
        {
            public DroneStat  Stat;
            public GameObject Root;          // whole meter row, toggled owned/acquirable
            public Text       NameLabel;     // "Cargo"
            public Transform  PipParent;     // holds the level pips (cleared + repopulated on bind)
            public TMP_Text       CostLabel;     // "760" / "MAX"
            public Button     UpgradeButton; // "+"
        }

        [Header("Card")]
        [SerializeField] private Image  _icon;
        [SerializeField] private Text   _label;
        [SerializeField] private Button _setActiveButton;
        [SerializeField] private TMP_Text   _setActiveLabel;
        [SerializeField] private Button _acquireButton;
        [SerializeField] private TMP_Text   _acquireLabel;

        [Header("Stat meters")]
        [SerializeField] private StatMeter[] _statMeters;

        [Header("Acquirable comparison")]
        [SerializeField] private TMP_Text _tierCallout; // "Mines up to Tier N asteroids" (acquirable only)

        [Header("Pip sprites (assign your art)")]
        [SerializeField] private Image  _pipTemplate;   // a single inactive pip Image, cloned per level
        [SerializeField] private Sprite _pipOnSprite;   // an upgraded level
        [SerializeField] private Sprite _pipOffSprite;  // a not-yet-upgraded level

        // Configure this card for an owned drone: icon + title + set-active + per-stat meters.
        public void BindOwned(Sprite icon, string title, bool isActive, Action onSetActive, IReadOnlyList<DroneStatVm> stats)
        {
            SetIcon(icon);
            if (_label != null) _label.text = title;
            SetVisible(_acquireButton, false);
            if (_tierCallout != null) _tierCallout.gameObject.SetActive(false);

            if (_setActiveButton != null)
            {
                _setActiveButton.gameObject.SetActive(true);
                _setActiveButton.interactable = !isActive;
                if (_setActiveLabel != null) _setActiveLabel.text = isActive ? "Selected" : "Select";
                Rewire(_setActiveButton, onSetActive);
            }

            if (_statMeters != null)
            {
                foreach (var m in _statMeters)
                {
                    if (m == null) continue;
                    if (m.Root != null) m.Root.SetActive(true);

                    var vm = Find(stats, m.Stat);
                    if (m.NameLabel != null)
                    {
                        m.NameLabel.text  = m.Stat.ToString();
                        m.NameLabel.color = Color.white; // undo any comparison-mode tint
                    }
                    BuildPips(m.PipParent, vm.Level, vm.MaxLevel);
                    if (m.CostLabel != null) m.CostLabel.text = vm.Maxed ? "MAX" : vm.Cost.ToString();

                    if (m.UpgradeButton != null)
                    {
                        m.UpgradeButton.gameObject.SetActive(true);
                        m.UpgradeButton.interactable = !vm.Maxed && vm.CanAfford;
                        Rewire(m.UpgradeButton, vm.OnUpgrade);
                    }
                }
            }
        }

        // Configure this card for an acquirable (not-yet-owned) drone: icon + title + a "why buy this?"
        // comparison against the active drone (per-stat "old → new" deltas + a tier callout) + acquire.
        // The three stat-meter rows are reused in comparison mode: the CostLabel shows "from → to" and
        // the pips/upgrade button are suppressed. (Cards are freshly instantiated each rebuild, so no
        // owned-mode styling bleeds in.)
        public void BindAcquirable(Sprite icon, string title, bool interactable, Action onAcquire,
            IReadOnlyList<DroneStatDeltaVm> comparison, string tierText, DeltaDirection tierDirection)
        {
            SetIcon(icon);
            if (_label != null) _label.text = title;

            SetVisible(_setActiveButton, false);

            if (_statMeters != null)
            {
                foreach (var m in _statMeters)
                {
                    if (m == null) continue;
                    if (m.Root != null) m.Root.SetActive(true);

                    var vm = FindDelta(comparison, m.Stat);
                    // The CostLabel lives *inside* the upgrade button (hidden below), so the delta goes
                    // in the always-visible name label instead: e.g. "Cargo   50 → 120".
                    if (m.NameLabel != null)
                    {
                        m.NameLabel.text  = $"{m.Stat}   {vm.FromText} → {vm.ToText}";
                        m.NameLabel.color = DeltaColor(vm.Direction);
                    }
                    ClearPips(m.PipParent);
                    SetVisible(m.UpgradeButton, false);
                }
            }

            if (_tierCallout != null)
            {
                _tierCallout.gameObject.SetActive(true);
                _tierCallout.text  = tierText;
                _tierCallout.color = DeltaColor(tierDirection);
            }

            if (_acquireButton != null)
            {
                _acquireButton.gameObject.SetActive(true);
                _acquireButton.interactable = interactable;
                if (_acquireLabel != null) _acquireLabel.text = "Unlock";
                Rewire(_acquireButton, onAcquire);
            }
        }

        private void SetIcon(Sprite icon)
        {
            if (_icon == null) return;
            _icon.sprite = icon;
            _icon.enabled = icon != null; // hide the Image when a drone has no icon assigned yet
        }

        // Rebuild the level pips: `maxLevel` pips, the first `level` using the on-sprite, the rest off.
        private void BuildPips(Transform parent, int level, int maxLevel)
        {
            if (parent == null || _pipTemplate == null) return;

            ClearPips(parent);

            for (int i = 0; i < maxLevel; i++)
            {
                var pip = Instantiate(_pipTemplate, parent);
                pip.gameObject.SetActive(true);
                pip.sprite = i < level ? _pipOnSprite : _pipOffSprite;
            }
        }

        private void ClearPips(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }

        private static DroneStatDeltaVm FindDelta(IReadOnlyList<DroneStatDeltaVm> deltas, DroneStat stat)
        {
            string key = stat.ToString();
            if (deltas != null)
                for (int i = 0; i < deltas.Count; i++)
                    if (deltas[i].Label == key) return deltas[i];
            return new DroneStatDeltaVm(key, "—", "—", DeltaDirection.Same);
        }

        // Green = better, red = worse, muted = unchanged. Tuned for readability on the dark card.
        private static Color DeltaColor(DeltaDirection dir)
        {
            switch (dir)
            {
                case DeltaDirection.Up:   return new Color(0.42f, 0.83f, 0.46f);
                case DeltaDirection.Down: return new Color(0.90f, 0.45f, 0.45f);
                default:                  return new Color(0.78f, 0.78f, 0.78f);
            }
        }

        private static DroneStatVm Find(IReadOnlyList<DroneStatVm> stats, DroneStat stat)
        {
            if (stats != null)
                for (int i = 0; i < stats.Count; i++)
                    if (stats[i].Stat == stat) return stats[i];
            return new DroneStatVm(stat, 0, 0, 0, false, false, null);
        }

        private static void Rewire(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }

        private static void SetVisible(Button button, bool visible)
        {
            if (button != null) button.gameObject.SetActive(visible);
        }
    }
}
