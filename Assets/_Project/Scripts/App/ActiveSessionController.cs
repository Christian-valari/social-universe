using System;
using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Social;
using SocialUniverse.UI;

namespace SocialUniverse.App
{
    // One active device per account (ServerCode/ClaimSession.js). Every Cloud Code call is checked
    // server-side, but a replaced device that is idle makes none — so while in the game this
    // re-checks the session every CheckIntervalSeconds and as soon as the app regains focus. When
    // BackendClient reports SessionReplacedEvent it leaves chat and shows a dialog whose only way
    // out is Quit. The device stays signed in: reopening the app signs in again and takes the account
    // back (AuthService claims a fresh session on every sign-in — newest launch wins). Boot, Auth and
    // Logout are left alone: they have no game session to end.
    public class ActiveSessionController : IStartable, ITickable, IDisposable
    {
        private const float CheckIntervalSeconds = 60f;

        private readonly IBackendClient   _backend;
        private readonly GameStateMachine _fsm;
        private readonly IChatService     _chat;

        private float _nextCheckAt;
        private bool  _checking;
        private bool  _replaced;

        public ActiveSessionController(IBackendClient backend, GameStateMachine fsm, IChatService chat)
        {
            _backend = backend;
            _fsm     = fsm;
            _chat    = chat;
        }

        public void Start()
        {
            EventBus.Subscribe<SessionReplacedEvent>(OnSessionReplaced);
            Application.focusChanged += OnFocusChanged;
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<SessionReplacedEvent>(OnSessionReplaced);
            Application.focusChanged -= OnFocusChanged;
        }

        public void Tick()
        {
            if (_checking || _backend.SessionId == null || !InGame || Time.realtimeSinceStartup < _nextCheckAt) return;
            _nextCheckAt = Time.realtimeSinceStartup + CheckIntervalSeconds;
            _ = CheckAsync();
        }

        private bool InGame => _fsm.Current is not (null or BootState or AuthState or LogoutState);

        private void OnFocusChanged(bool focused)
        {
            if (focused) _nextCheckAt = 0f;
        }

        // ClaimSession with the current id only confirms it; a replaced session comes back as
        // SESSION_REPLACED, which BackendClient turns into SessionReplacedEvent. Network errors
        // just wait for the next check — they never sign the player out.
        private async Task CheckAsync()
        {
            _checking = true;
            try
            {
                await _backend.CallAsync<string>("ClaimSession");
            }
            catch (Exception ex)
            {
                SULog.Warn($"ActiveSessionController: session check failed ({ex.Message})", SULog.Channel.Net);
            }
            finally
            {
                _checking = false;
            }
        }

        // Every rejected call reports the replacement; act on the first. Leaving chat keeps the
        // replaced device out of the planet's roster (presence) while the dialog waits for Quit.
        private async void OnSessionReplaced(SessionReplacedEvent _)
        {
            if (!InGame || _replaced) return;
            _replaced = true;
            SULog.Info("ActiveSessionController: account signed in on another device — asking the player to quit", SULog.Channel.Net);
            QuitDialogView.Show("Your account was signed in on another device.");
            try
            {
                await _chat.DisconnectAsync();
            }
            catch (Exception ex)
            {
                SULog.Warn($"ActiveSessionController: chat disconnect failed ({ex.Message})", SULog.Channel.Net);
            }
        }
    }
}
