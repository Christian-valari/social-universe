using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    public class AuthScreen : MonoBehaviour
    {
        // --- Panels ---
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;

        // --- Login panel ---
        [SerializeField] private InputField _loginUsernameField;
        [SerializeField] private InputField _loginPasswordField;
        [SerializeField] private Text       _loginStatusText;
        [SerializeField] private Button     _loginButton;
        [SerializeField] private Button     _guestButton;
        [SerializeField] private Button     _goToRegisterButton;

        // --- Register panel ---
        [SerializeField] private InputField _regUsernameField;
        [SerializeField] private InputField _regPasswordField;
        [SerializeField] private InputField _regConfirmField;
        [SerializeField] private InputField _regDisplayNameField;
        [SerializeField] private Text       _regStatusText;
        [SerializeField] private Button     _registerButton;
        [SerializeField] private Button     _goToLoginButton;

        private IAuthService _auth;
        private bool         _busy;

        [Inject]
        public void Construct(IAuthService auth) => _auth = auth;

        private void Start()
        {
            _auth.OnSignedIn     += HandleSignedIn;
            _auth.OnSignInFailed += HandleSignInFailed;

            _loginButton       .onClick.AddListener(OnLoginClicked);
            _guestButton       .onClick.AddListener(OnGuestClicked);
            _goToRegisterButton.onClick.AddListener(() => ShowPanel(false));
            _registerButton    .onClick.AddListener(OnRegisterClicked);
            _goToLoginButton   .onClick.AddListener(() => ShowPanel(true));

            if (_auth.IsSignedIn)
                HandleSignedIn();
            else
                ShowPanel(true);
        }

        private void OnDestroy()
        {
            if (_auth == null) return;
            _auth.OnSignedIn     -= HandleSignedIn;
            _auth.OnSignInFailed -= HandleSignInFailed;
        }

        // true = LoginPanel, false = RegisterPanel
        private void ShowPanel(bool login)
        {
            _loginPanel   .SetActive(login);
            _registerPanel.SetActive(!login);
            _loginStatusText.text = "";
            _regStatusText  .text = "";
        }

        // -------------------------------------------------------------------------
        private void HandleSignedIn()
        {
            SetBusy(false);
            SULog.Info("Auth: signed in — advancing to game", SULog.Channel.Net);
            EventBus.Publish(new PlayerReadyEvent());
        }

        private void HandleSignInFailed(Exception ex)
        {
            SetActiveStatus(FriendlyError(ex));
            SetBusy(false);
        }

        // -------------------------------------------------------------------------
        private async void OnLoginClicked()
        {
            string username = _loginUsernameField.text.Trim();
            string password = _loginPasswordField.text;

            if (!ValidateCredentials(username, password, out string err))
            {
                _loginStatusText.text = err;
                return;
            }

            SetBusy(true);
            _loginStatusText.text = "Signing in…";
            try   { await _auth.SignInWithCredentialsAsync(username, password); }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnGuestClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in as guest…";
            try   { await _auth.SignInAnonymouslyAsync(); }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnRegisterClicked()
        {
            string username    = _regUsernameField.text.Trim();
            string password    = _regPasswordField.text;
            string confirm     = _regConfirmField .text;
            string displayName = _regDisplayNameField != null ? _regDisplayNameField.text.Trim() : username;

            if (!ValidateCredentials(username, password, out string err))
            {
                _regStatusText.text = err;
                return;
            }
            if (password != confirm)
            {
                _regStatusText.text = "Passwords do not match";
                return;
            }
            if (!ValidateDisplayName(displayName, out string nameErr))
            {
                _regStatusText.text = nameErr;
                return;
            }

            SetBusy(true);
            _regStatusText.text = "Creating account…";
            try   { await _auth.RegisterAsync(username, password, displayName); }
            catch (Exception ex) { _regStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        // -------------------------------------------------------------------------
        private void SetBusy(bool busy)
        {
            _busy = busy;
            _loginButton   .interactable = !busy;
            _guestButton   .interactable = !busy;
            _registerButton.interactable = !busy;
        }

        private void SetActiveStatus(string message)
        {
            if (_loginPanel.activeSelf)
                _loginStatusText.text = message;
            else
                _regStatusText.text = message;
        }

        private static bool ValidateCredentials(string username, string password, out string error)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                error = "Username must be at least 3 characters";
                return false;
            }
            if (password.Length < 6)
            {
                error = "Password must be at least 6 characters";
                return false;
            }
            error = null;
            return true;
        }

        private static bool ValidateDisplayName(string displayName, out string error)
        {
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length < 2)
            {
                error = "Display name must be at least 2 characters";
                return false;
            }
            if (displayName.Length > 20)
            {
                error = "Display name must be 20 characters or fewer";
                return false;
            }
            error = null;
            return true;
        }

        private static string FriendlyError(Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("already taken") || msg.Contains("EntityExists") || msg.Contains("ENTITY_EXISTS"))
                return "Username already taken";
            if (msg.Contains("INVALID_USERNAME") || msg.Contains("Invalid username"))
                return "Invalid username — use letters, numbers, hyphens and underscores";
            if (msg.Contains("INVALID_PASSWORD") || msg.Contains("wrong password") || msg.Contains("Incorrect"))
                return "Incorrect username or password";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
            return msg;
        }
    }
}
