using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // View-model for one upgradeable-stat button on a drone row. DroneGarageView builds these
    // (it owns the domain math); DroneRowView just presents them.
    public readonly struct DroneUpgradeVm
    {
        public readonly DroneStat Stat;
        public readonly string    Caption;
        public readonly bool      Interactable;
        public readonly Action    OnClick;

        public DroneUpgradeVm(DroneStat stat, string caption, bool interactable, Action onClick)
        {
            Stat         = stat;
            Caption      = caption;
            Interactable = interactable;
            OnClick      = onClick;
        }
    }

    // One row in the unified Drone Garage list. A single prefab serves BOTH an owned drone
    // (set-active + per-stat upgrade buttons) and an acquirable drone (acquire button); the
    // widgets not relevant to the current bind are hidden. The row owns its child-widget
    // references via the Inspector, so DroneGarageView never reaches into the prefab by name.
    public class DroneRowView : MonoBehaviour
    {
        // One upgrade button on the row, tagged with the stat it drives.
        [Serializable]
        public class UpgradeButton
        {
            public DroneStat Stat;
            public Button    Button;
            public Text      Label;
        }

        [SerializeField] private Text            _label;
        [SerializeField] private Button          _setActiveButton;
        [SerializeField] private Text            _setActiveLabel;
        [SerializeField] private UpgradeButton[] _upgradeButtons;
        [SerializeField] private Button          _acquireButton;
        [SerializeField] private Text            _acquireLabel;

        // Configure this row for an owned drone: title + set-active state + per-stat upgrades.
        public void BindOwned(string title, bool isActive, Action onSetActive, IReadOnlyList<DroneUpgradeVm> upgrades)
        {
            if (_label != null) _label.text = title;

            SetVisible(_acquireButton, false);

            if (_setActiveButton != null)
            {
                _setActiveButton.gameObject.SetActive(true);
                _setActiveButton.interactable = !isActive;
                if (_setActiveLabel != null) _setActiveLabel.text = isActive ? "Active" : "Set Active";
                Rewire(_setActiveButton, onSetActive);
            }

            if (_upgradeButtons != null)
            {
                foreach (var ub in _upgradeButtons)
                {
                    if (ub == null || ub.Button == null) continue;
                    var vm = Find(upgrades, ub.Stat);
                    ub.Button.gameObject.SetActive(true);
                    ub.Button.interactable = vm.Interactable;
                    if (ub.Label != null) ub.Label.text = vm.Caption;
                    Rewire(ub.Button, vm.OnClick);
                }
            }
        }

        // Configure this row for an acquirable (not-yet-owned) drone: title + acquire button only.
        public void BindAcquirable(string title, bool interactable, Action onAcquire)
        {
            if (_label != null) _label.text = title;

            SetVisible(_setActiveButton, false);
            if (_upgradeButtons != null)
                foreach (var ub in _upgradeButtons)
                    if (ub?.Button != null) ub.Button.gameObject.SetActive(false);

            if (_acquireButton != null)
            {
                _acquireButton.gameObject.SetActive(true);
                _acquireButton.interactable = interactable;
                if (_acquireLabel != null) _acquireLabel.text = "Acquire";
                Rewire(_acquireButton, onAcquire);
            }
        }

        private static DroneUpgradeVm Find(IReadOnlyList<DroneUpgradeVm> upgrades, DroneStat stat)
        {
            if (upgrades != null)
                for (int i = 0; i < upgrades.Count; i++)
                    if (upgrades[i].Stat == stat) return upgrades[i];
            return new DroneUpgradeVm(stat, "-", false, null);
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
