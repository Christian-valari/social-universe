using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Mining;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Shown when the player taps an asteroid: lets them choose how to mine it.
    public class MiningModePromptView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text       _titleText;
        [SerializeField] private Button     _idleMineButton;
        [SerializeField] private Button     _activeMineButton;

        [Inject] private MiningController _mining;

        private Asteroid _pendingAsteroid;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);

            if (_idleMineButton   != null) _idleMineButton.onClick.AddListener(OnIdleMineClicked);
            if (_activeMineButton != null) _activeMineButton.onClick.AddListener(OnActiveMineClicked);
        }

        private void Start() => EventBus.Subscribe<AsteroidSelectedEvent>(OnAsteroidSelected);

        private void OnDestroy() => EventBus.Unsubscribe<AsteroidSelectedEvent>(OnAsteroidSelected);

        private void OnAsteroidSelected(AsteroidSelectedEvent e)
        {
            var asteroid = e.Asteroid;
            if (asteroid == null || asteroid.IsDepleted) return;
            if (_mining.CurrentIdleSession != null) return;  // drone already busy on a session
            if (_mining.ClaimingAsteroid   == asteroid) return; // final claim tap just completed

            _pendingAsteroid = asteroid;
            if (_titleText != null)
                _titleText.text = $"Mine {asteroid.Definition.MineralType}?";

            if (_root != null) _root.SetActive(true);
        }

        private void OnIdleMineClicked()
        {
            if (_pendingAsteroid != null)
                _mining.BeginIdleMining(_pendingAsteroid);

            ClosePrompt();
        }

        private void OnActiveMineClicked()
        {
            // Active mining mini-game arrives in a later milestone — no-op for now.
            SULog.Info("Active mining mode chosen — mini-game coming in a later milestone", SULog.Channel.Mining);
            ClosePrompt();
        }

        private void ClosePrompt()
        {
            _pendingAsteroid = null;
            if (_root != null) _root.SetActive(false);
        }
    }
}
