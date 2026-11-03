using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SocialUniverse.UI
{
    // Reusable confirm popup for purchase / remove. Show() wires the confirm callback;
    // Confirm invokes it then hides, Cancel just hides.
    public class HexBuildPopup : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text   _message;
        [SerializeField] private Button      _confirm;
        [SerializeField] private Button      _cancel;
        [SerializeField] private RectTransform _window;   // the panel that pops in and out (PopupTransition)

        private Action _onConfirm;

        private PopupTransition _transition;
        private PopupTransition Transition => _transition != null ? _transition : _transition = PopupTransition.On(_root, _window);

        private void Awake()
        {
            _confirm.onClick.AddListener(() => { _onConfirm?.Invoke(); Hide(); });
            _cancel.onClick.AddListener(Hide);
            if (_root != null) _root.SetActive(false);
        }

        public void Show(string message, Action onConfirm)
        {
            if (_message != null) _message.text = message;
            _onConfirm = onConfirm;
            if (_root != null) Transition.Show();
        }

        public void Hide()
        {
            _onConfirm = null;
            if (_root != null) Transition.Hide();
        }
    }
}
