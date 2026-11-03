using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Config;
using SocialUniverse.Mining;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    // Shown when the player taps an asteroid: shows its tier and whether the active drone can mine
    // it — or is busy on another asteroid (AsteroidTierPrompt) — and lets them choose how to mine it,
    // with what idle and active mining pay.
    // Tapping the asteroid the drone is already mining shows a toast instead. Opens and closes with
    // PopupTransition's pop & fade.
    public class MiningModePromptView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text   _titleText;
        [SerializeField] private TMP_Text   _tierStatusText;   // whether the active drone can mine it, then what each mode pays
        [SerializeField] private Button     _idleMineButton;
        [SerializeField] private Button     _activeMineButton;
        [SerializeField] private RectTransform _window;        // the prompt panel that pops in and out

        [Inject] private MiningController _mining;
        [Inject] private DroneFleet       _fleet;

        // Status line colours, the same green/red as the garage's stat comparison (DroneRowView).
        private static readonly Color CanMineColor  = new Color(0.42f, 0.83f, 0.46f);
        private static readonly Color CantMineColor = new Color(0.90f, 0.45f, 0.45f);

        private Asteroid _pendingAsteroid;

        private PopupTransition _transition;
        private PopupTransition Transition => _transition != null ? _transition : _transition = PopupTransition.On(_root, _window);

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
            if (_mining.ClaimingAsteroid == asteroid) return; // final claim tap just completed

            var session = _mining.CurrentIdleSession;
            if (session != null && session.Asteroid == asteroid)
            {
                // A tap once it's ReadyToClaim is the claim itself (IdleMiningSessionController).
                if (session.Stage != IdleMiningStage.ReadyToClaim) ToastView.Show(AsteroidTierPrompt.MiningThisAsteroid);
                return;
            }
            if (_mining.StartingAsteroid == asteroid)
            {
                ToastView.Show(AsteroidTierPrompt.MiningThisAsteroid); // its start is with the server
                return;
            }

            _pendingAsteroid = asteroid;
            ShowTier(asteroid);

            if (_root != null) Transition.Show();
        }

        // MiningController still enforces the tier gate and the one-session-at-a-time rule; disabling
        // the buttons just saves the player a tap that would only be refused.
        private void ShowTier(Asteroid asteroid)
        {
            var def      = asteroid.Definition;
            var drone    = _fleet?.Active;
            var session  = _mining.CurrentIdleSession;
            // Busy: mining, or about to (a start the server hasn't answered); returning: a claim the
            // server hasn't answered. MiningController refuses a new start in all three cases.
            var busy     = session != null ? session.Asteroid : _mining.StartingAsteroid;
            var claiming = _mining.ClaimingAsteroid;
            var verdict  = AsteroidTierPrompt.For(MineralName(def), def.Tier,
                drone?.Definition.DisplayName, drone != null ? drone.Definition.Tier : 0,
                busy != null ? MineralName(busy.Definition) : null,
                session != null && session.Stage == IdleMiningStage.ReadyToClaim,
                claiming != null ? MineralName(claiming.Definition) : null);

            if (_titleText != null) _titleText.text = verdict.Title;
            if (_tierStatusText != null)
            {
                // What each way of mining pays, only when the player can pick one.
                _tierStatusText.text  = verdict.CanMine
                    ? verdict.Status + "\n" + AsteroidTierPrompt.PayLines(_mining.IdleYieldFraction)
                    : verdict.Status;
                _tierStatusText.color = verdict.CanMine ? CanMineColor : CantMineColor;
            }
            if (_idleMineButton   != null) _idleMineButton.interactable   = verdict.CanMine;
            if (_activeMineButton != null) _activeMineButton.interactable = verdict.CanMine;
        }

        private static string MineralName(AsteroidDefinition def) =>
            def.Mineral != null ? def.Mineral.DisplayName : def.MineralType;

        private void OnIdleMineClicked()
        {
            // Fire-and-forget: the drone leaves once the server has recorded the run (a refusal shows
            // a toast through MiningClaimRejectedEvent; anything unexpected is logged).
            if (_pendingAsteroid != null)
                _mining.BeginIdleMiningAsync(_pendingAsteroid).Forget("Starting idle mining", SULog.Channel.Mining);

            ClosePrompt();
        }

        private void OnActiveMineClicked()
        {
            if (_pendingAsteroid != null && _mining.BeginActiveMining(_pendingAsteroid))
                EventBus.Publish(new ActiveMiningRequestedEvent());

            ClosePrompt();
        }

        public void ClosePrompt()
        {
            _pendingAsteroid = null;
            if (_root != null) Transition.Hide();
        }
    }
}
