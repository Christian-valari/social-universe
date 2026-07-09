using System.Collections;
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

        private Wallet    _wallet;
        private int       _displayedCoins;
        private Coroutine _coinsCountUpRoutine;

        public void Bind(Wallet wallet)
        {
            Unbind();
            _wallet = wallet;
            _wallet.OnCoinsChanged    += SetCoins;
            _wallet.OnStardustChanged += SetStardust;
            SetCoinsInstant(_wallet.Coins);
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

        // Coin gains count up over half a second so a yield claim (or any coin gain) reads as a
        // reward instead of an instant number swap. Losses (spends) and the initial Bind snapshot
        // still snap instantly — only upward changes are worth animating.
        private void SetCoins(int amount)
        {
            if (_coinsText == null) return;

            if (amount > _displayedCoins)
            {
                if (_coinsCountUpRoutine != null) StopCoroutine(_coinsCountUpRoutine);
                _coinsCountUpRoutine = StartCoroutine(CountUpCoins(_displayedCoins, amount));
            }
            else
            {
                SetCoinsInstant(amount);
            }
        }

        private void SetCoinsInstant(int amount)
        {
            _displayedCoins = amount;
            if (_coinsText != null) _coinsText.text = amount.ToString("N0");
        }

        private IEnumerator CountUpCoins(int from, int to)
        {
            const float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _displayedCoins = Mathf.RoundToInt(Mathf.Lerp(from, to, elapsed / duration));
                _coinsText.text = _displayedCoins.ToString("N0");
                yield return null;
            }
            SetCoinsInstant(to);
            _coinsCountUpRoutine = null;
        }

        private void SetStardust(int amount) { if (_stardustText != null) _stardustText.text = amount.ToString("N0"); }
    }
}
