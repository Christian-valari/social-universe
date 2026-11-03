using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Safety;

namespace SocialUniverse.UI
{
    // Shared confirm → busy → result popup for coin transactions: drone and slot purchases in the
    // Drone Garage, mineral sales in the Mineral Inventory. A passive view: the owning panel supplies
    // each screen (from its prompt helper), sends its request in onConfirm, and calls ShowSuccess /
    // ShowFailure when its result event arrives. Saved inactive under its panel: Awake wires the
    // buttons on first Open. Opens and closes with PopupTransition's pop & fade. One instance per
    // panel; PlanetSceneScope injects each.
    public class TransactionModal : MonoBehaviour
    {
        [SerializeField] private Image    _icon;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;
        [FormerlySerializedAs("_costText")]
        [SerializeField] private TMP_Text _amountText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button   _confirmButton;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private Button   _cancelButton;
        [SerializeField] private Button   _okButton;
        [SerializeField] private RectTransform _window;   // the part that pops in and out (PopupTransition)
        [SerializeField] private GameObject _iconGlow;    // shine behind the icon; hidden with it

        [Inject] private IAudioManager _audio;

        private Action _onConfirm;
        private string _busyText;

        private PopupTransition _transition;
        private PopupTransition Transition => _transition != null ? _transition : _transition = PopupTransition.On(gameObject, _window);

        // True from Confirm until the owner reports the result; the popup can't be closed meanwhile.
        public bool IsBusy { get; private set; }

        private void Awake()
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _cancelButton.onClick.AddListener(() => Hide(SfxId.Cancel));
            _okButton.onClick.AddListener(() => Hide(SfxId.Confirm));
        }

        public void Open(TransactionScreen confirm, Sprite icon, string busyText, Action onConfirm)
        {
            _onConfirm = onConfirm;
            _busyText  = busyText;
            IsBusy     = false;

            SetTexts(confirm);
            _icon.sprite  = icon;
            _icon.enabled = icon != null;
            if (_iconGlow != null) _iconGlow.SetActive(icon != null); // a fleet slot has no icon to light
            _confirmLabel.text = confirm.ConfirmLabel;
            ShowButtons(result: false);
            _confirmButton.interactable = confirm.ConfirmEnabled;
            _cancelButton.interactable  = true;

            _audio?.PlaySfx(SfxId.OpenPanel);
            Transition.Show();
        }

        public void ShowSuccess(TransactionScreen result)
        {
            SetBusy(false);
            _audio?.PlaySfx(SfxId.CoinsReward);
            SetTexts(result);
            ShowButtons(result: true);
        }

        public void ShowFailure(string message)
        {
            SetBusy(false);
            _statusText.text = message; // Confirm and Cancel are live again: retry or leave
        }

        private void OnConfirmClicked()
        {
            if (IsBusy) return;
            _audio?.PlaySfx(SfxId.Confirm);
            SetBusy(true);
            _statusText.text = _busyText;
            _onConfirm?.Invoke(); // the result may arrive synchronously, inside this call
        }

        private void Hide(SfxId sfx)
        {
            if (IsBusy) return; // the request is in flight; its result must land somewhere
            _audio?.PlaySfx(sfx);
            Transition.Hide();
        }

        private void SetTexts(TransactionScreen s)
        {
            _titleText.text  = s.Title;
            _bodyText.text   = s.Body;
            _amountText.text = s.Amount;
            _statusText.text = s.Status;
        }

        private void ShowButtons(bool result)
        {
            _confirmButton.gameObject.SetActive(!result);
            _cancelButton.gameObject.SetActive(!result);
            _okButton.gameObject.SetActive(result);
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            _confirmButton.interactable = !busy;
            _cancelButton.interactable  = !busy;
        }
    }
}
