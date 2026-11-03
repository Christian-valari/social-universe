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
    // (icon + per-stat pip meters with a "+" upgrade button) and an acquirable drone (icon + stat
    // comparison); the widgets not relevant to the current bind are hidden. An acquirable card has
    // no buy button of its own: the garage's buy button follows the centered card. The card owns
    // its child-widget references via the Inspector, so DroneGarageView never reaches in by name.
    // The frame, tier badge and state chip tell owned, active and locked cards apart at a glance.
    public class DroneRowView : MonoBehaviour
    {
        // One upgrade meter on the card, tagged with the stat it drives.
        [Serializable]
        public class StatMeter
        {
            public DroneStat  Stat;
            public GameObject Root;          // whole meter row, toggled owned/acquirable
            public TMP_Text   NameLabel;     // "Cargo"
            public Transform  PipParent;     // holds the level pips (cleared + repopulated on bind)
            public TMP_Text   CostLabel;     // "760" / "MAX"
            public GameObject CostIcon;      // coin beside the price, hidden at MAX
            public Button     UpgradeButton; // "+"
        }

        [Header("Card")]
        [SerializeField] private Image    _icon;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Button   _setActiveButton;
        [SerializeField] private TMP_Text _setActiveLabel;

        [Header("Card state")]
        [SerializeField] private Graphic    _frameBorder;  // card outline, tinted by state
        [SerializeField] private Image      _tierBadge;    // hexagon behind the tier, tinted per tier
        [SerializeField] private TMP_Text   _tierLabel;    // "T3"
        [SerializeField] private Graphic    _stateChip;    // pill behind the state label
        [SerializeField] private TMP_Text   _stateLabel;   // "Active" / "Owned" / "Locked"
        [SerializeField] private GameObject _lockIcon;     // shown on locked cards only

        [Header("Stat meters")]
        [SerializeField] private StatMeter[] _statMeters;

        [Header("Acquirable comparison")]
        [SerializeField] private TMP_Text _tierCallout; // "Mines up to Tier N asteroids" (acquirable only)

        [Header("Pip sprites (assign your art)")]
        [SerializeField] private Image  _pipTemplate;   // a single inactive pip Image, cloned per level
        [SerializeField] private Sprite _pipOnSprite;   // an upgraded level
        [SerializeField] private Sprite _pipOffSprite;  // a not-yet-upgraded level
        [SerializeField] private Color  _pipOnColor  = Color.white;
        [SerializeField] private Color  _pipOffColor = new Color(1f, 1f, 1f, 0.25f);

        [Header("State colours")]
        [SerializeField] private Color   _activeColor      = new Color(0.49f, 0.96f, 0.78f); // mint
        [SerializeField] private Color   _ownedColor       = new Color(0.56f, 0.42f, 0.91f); // lilac
        [SerializeField] private Color   _lockedColor      = new Color(1.00f, 0.78f, 0.34f); // amber, like the coin
        [SerializeField] private Color   _lockedFrameColor = new Color(0.29f, 0.24f, 0.48f);
        [SerializeField] private Color   _lockedIconTint   = new Color(0.55f, 0.55f, 0.66f); // dims a drone not yet owned
        [SerializeField] private float   _stateChipAlpha   = 0.18f;
        [SerializeField] private Color   _statNameColor    = Color.white;

        // Scratch list for the pips of the meter being bound; reused to keep binds allocation-free.
        private readonly List<Image> _pips = new List<Image>();

        // Configure this card for an owned drone: icon + name + tier + set-active + per-stat meters.
        public void BindOwned(Sprite icon, string title, int tier, bool isActive, Action onSetActive, IReadOnlyList<DroneStatVm> stats)
        {
            SetIcon(icon, Color.white);
            if (_label != null) _label.text = title;
            SetTier(tier);
            SetState(isActive ? "Active" : "Owned", isActive ? _activeColor : _ownedColor, isActive ? _activeColor : _ownedColor, locked: false);
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
                    bool offered = Contains(stats, m.Stat);
                    if (m.Root != null) m.Root.SetActive(offered);
                    if (!offered) continue;

                    var vm = Find(stats, m.Stat);
                    if (m.NameLabel != null)
                    {
                        m.NameLabel.text  = m.Stat.ToString();
                        m.NameLabel.color = _statNameColor; // undo any comparison-mode tint
                    }
                    BuildPips(m.PipParent, vm.Level, vm.MaxLevel);
                    if (m.CostLabel != null) m.CostLabel.text = vm.Maxed ? "MAX" : vm.Cost.ToString();
                    if (m.CostIcon != null) m.CostIcon.SetActive(!vm.Maxed);

                    if (m.UpgradeButton != null)
                    {
                        m.UpgradeButton.gameObject.SetActive(true);
                        m.UpgradeButton.interactable = !vm.Maxed && vm.CanAfford;
                        Rewire(m.UpgradeButton, vm.OnUpgrade);
                    }
                }
            }
        }

        // Configure this card for an acquirable (not-yet-owned) drone: icon + name + tier + a "why buy
        // this?" comparison against the active drone (per-stat "old → new" deltas + a tier callout).
        // The stat-meter rows are reused in comparison mode: the CostLabel shows "from → to" and
        // the pips/upgrade button are suppressed. (Cards are freshly instantiated each rebuild, so no
        // owned-mode styling bleeds in.)
        public void BindAcquirable(Sprite icon, string title, int tier,
            IReadOnlyList<DroneStatDeltaVm> comparison, string tierText, DeltaDirection tierDirection)
        {
            SetIcon(icon, _lockedIconTint);
            if (_label != null) _label.text = title;
            SetTier(tier);
            SetState("Locked", _lockedColor, _lockedFrameColor, locked: true);

            SetVisible(_setActiveButton, false);

            if (_statMeters != null)
            {
                foreach (var m in _statMeters)
                {
                    if (m == null) continue;
                    bool offered = ContainsDelta(comparison, m.Stat);
                    if (m.Root != null) m.Root.SetActive(offered);
                    if (!offered) continue;

                    var vm = FindDelta(comparison, m.Stat);
                    // The CostLabel lives *inside* the upgrade button (hidden below), so the delta goes
                    // in the always-visible name label instead: e.g. "Yield   1.0x → 1.5x".
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
        }

        private void SetIcon(Sprite icon, Color tint)
        {
            if (_icon == null) return;
            _icon.sprite = icon;
            _icon.color  = tint;
            _icon.enabled = icon != null; // hide the Image when a drone has no icon assigned yet
        }

        private void SetTier(int tier)
        {
            if (_tierLabel != null) _tierLabel.text = $"T{tier}";
            if (_tierBadge != null) _tierBadge.color = TierColors.For(tier);
        }

        // The chip says what the card is; the frame repeats it in colour so a swipe reads at a glance.
        private void SetState(string label, Color color, Color frameColor, bool locked)
        {
            if (_stateLabel != null)
            {
                _stateLabel.text  = label;
                _stateLabel.color = color;
            }
            if (_stateChip != null)   _stateChip.color   = new Color(color.r, color.g, color.b, _stateChipAlpha);
            if (_frameBorder != null) _frameBorder.color = frameColor;
            if (_lockIcon != null)
            {
                _lockIcon.SetActive(locked);
                var lockGraphic = _lockIcon.GetComponent<Graphic>();
                if (lockGraphic != null) lockGraphic.color = color;
            }
        }

        // Set the level pips: `maxLevel` pips, the first `level` using the on-sprite, the rest off.
        // Pips are reused across binds — cards are re-bound on every fleet change now, and each
        // owned card carries MaxLevel pips per stat, so destroying and re-instantiating them was the
        // bulk of a refresh.
        private void BuildPips(Transform parent, int level, int maxLevel)
        {
            if (parent == null || _pipTemplate == null) return;

            CollectPips(parent);

            while (_pips.Count < maxLevel)
                _pips.Add(Instantiate(_pipTemplate, parent));

            for (int i = 0; i < _pips.Count; i++)
            {
                var pip = _pips[i];
                if (pip == null) continue;

                bool used = i < maxLevel;
                pip.gameObject.SetActive(used);
                if (!used) continue;

                bool reached = i < level;
                pip.sprite = reached ? _pipOnSprite : _pipOffSprite;
                pip.color  = reached ? _pipOnColor  : _pipOffColor;
            }
        }

        // Hide the pips without destroying them, so the next bind can reuse them.
        private void ClearPips(Transform parent)
        {
            if (parent == null) return;

            CollectPips(parent);
            foreach (var pip in _pips)
                if (pip != null) pip.gameObject.SetActive(false);
        }

        // The pips already under this meter, skipping the template if it happens to live there.
        private void CollectPips(Transform parent)
        {
            _pips.Clear();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (_pipTemplate != null && child == _pipTemplate.transform) continue;

                var pip = child.GetComponent<Image>();
                if (pip != null) _pips.Add(pip);
            }
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

        // A meter is shown only for stats the Garage offers (see DroneGarageView.OfferedUpgradeStats).
        private static bool Contains(IReadOnlyList<DroneStatVm> stats, DroneStat stat)
        {
            if (stats != null)
                for (int i = 0; i < stats.Count; i++)
                    if (stats[i].Stat == stat) return true;
            return false;
        }

        private static bool ContainsDelta(IReadOnlyList<DroneStatDeltaVm> deltas, DroneStat stat)
        {
            string key = stat.ToString();
            if (deltas != null)
                for (int i = 0; i < deltas.Count; i++)
                    if (deltas[i].Label == key) return true;
            return false;
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
