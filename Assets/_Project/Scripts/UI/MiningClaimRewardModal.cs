using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Safety;

namespace SocialUniverse.UI
{
    // "Claimed!" reward modal shown after the player taps a ready-to-claim asteroid and the
    // minerals have been granted. Opened by HUDController on IdleClaimCompletedEvent (this
    // modal is inactive by default and so cannot self-subscribe — same passive-view pattern
    // as LandPurchaseModal). Displays the mineral icon + "Name +Qty" and dismisses on Collect.
    public class MiningClaimRewardModal : MonoBehaviour
    {
        [SerializeField] private Image    _mineralIcon;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private Button   _collectButton;
        [SerializeField] private RectTransform _window;   // the panel that pops in and out (PopupTransition)

        [Inject] private DatabaseRegistry _registry;
        [Inject] private IAudioManager    _audio;

        private PopupTransition _transition;
        private PopupTransition Transition => _transition != null ? _transition : _transition = PopupTransition.On(gameObject, _window);

        private void Awake()
        {
            if (_collectButton != null) _collectButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open(string mineralId, int quantity)
        {
            var def = _registry != null ? _registry.GetMineral(mineralId) : null;

            _mineralIcon.sprite  = def.Icon;
            _rewardText.text = $"{def?.DisplayName ?? mineralId}  +{quantity}";

            _audio?.PlaySfx(SfxId.CoinsReward);
            Transition.Show();
        }

        public void Close()
        {
            _audio?.PlaySfx(SfxId.Confirm);
            Transition.Hide();
        }
    }
}
