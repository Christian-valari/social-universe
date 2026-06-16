using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Economy;
using TMPro;

namespace SocialUniverse.UI
{
    // Reusable currency readout — bound to a Wallet by its owner (HUD, shop, land sheet, ...).
    public class CurrencyView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private TMP_Text _stardustText;

        private Wallet _wallet;

        public void Bind(Wallet wallet)
        {
            Unbind();
            _wallet = wallet;
            _wallet.OnCoinsChanged    += SetCoins;
            _wallet.OnStardustChanged += SetStardust;
            SetCoins(_wallet.Coins);
            SetStardust(_wallet.Stardust);
        }

        public void Unbind()
        {
            if (_wallet == null) return;
            _wallet.OnCoinsChanged    -= SetCoins;
            _wallet.OnStardustChanged -= SetStardust;
            _wallet = null;
        }

        private void OnDestroy() => Unbind();

        private void SetCoins(int amount)    { if (_coinsText    != null) _coinsText.text    = amount.ToString("N0"); }
        private void SetStardust(int amount) { if (_stardustText != null) _stardustText.text = amount.ToString("N0"); }
    }
}
