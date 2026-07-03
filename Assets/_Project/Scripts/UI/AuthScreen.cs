using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Net;

namespace SocialUniverse.UI
{
    public class AuthScreen : MonoBehaviour
    {
        private enum AuthPanel { Login, Register, ForgotPassword }

        // --- Panels ---
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;
        [SerializeField] private GameObject _forgotPasswordPanel;

        // --- Login panel ---
        [SerializeField] private InputField _loginEmailField;
        [SerializeField] private InputField _loginPasswordField;
        [SerializeField] private Text       _loginStatusText;
        [SerializeField] private Button     _loginButton;
        [SerializeField] private Button     _guestButton;
        [SerializeField] private Button     _googleButton;
        [SerializeField] private Button     _goToRegisterButton;
        [SerializeField] private Button     _forgotPasswordButton;

        // --- Register panel ---
        [SerializeField] private InputField _regUsernameField;
        [SerializeField] private InputField _regEmailField;
        [SerializeField] private InputField _regPasswordField;
        [SerializeField] private InputField _regConfirmField;
        [SerializeField] private Text       _regStatusText;
        [SerializeField] private Button     _registerButton;
        [SerializeField] private Button     _goToLoginButton;

        // --- Forgot password panel ---
        [SerializeField] private InputField _forgotEmailField;
        [SerializeField] private InputField _forgotNewPasswordField;
        [SerializeField] private InputField _forgotConfirmField;
        [SerializeField] private InputField _forgotCodeField;
        [SerializeField] private Text       _forgotStatusText;
        [SerializeField] private Button     _sendResetCodeButton;
        [SerializeField] private Button     _resetPasswordButton;
        [SerializeField] private Button     _forgotBackToLoginButton;

        private IAuthService _auth;
        private bool         _busy;
        private bool         _suppressAutoTransition;

        [Inject]
        public void Construct(IAuthService auth) => _auth = auth;

        private void Start()
        {
            _auth.OnSignedIn     += HandleSignedIn;
            _auth.OnSignInFailed += HandleSignInFailed;

            _loginButton       .onClick.AddListener(OnLoginClicked);
            _guestButton       .onClick.AddListener(OnGuestClicked);
            _goToRegisterButton.onClick.AddListener(() => ShowPanel(AuthPanel.Register));
            _registerButton    .onClick.AddListener(OnRegisterClicked);
            _goToLoginButton   .onClick.AddListener(() => ShowPanel(AuthPanel.Login));

            if (_forgotPasswordButton   != null) _forgotPasswordButton  .onClick.AddListener(() => ShowPanel(AuthPanel.ForgotPassword));
            if (_forgotBackToLoginButton != null) _forgotBackToLoginButton.onClick.AddListener(() => ShowPanel(AuthPanel.Login));
            if (_sendResetCodeButton    != null) _sendResetCodeButton   .onClick.AddListener(OnSendResetCodeClicked);
            if (_resetPasswordButton    != null) _resetPasswordButton   .onClick.AddListener(OnResetPasswordClicked);
            if (_googleButton           != null) _googleButton          .onClick.AddListener(OnGoogleClicked);

            if (_auth.IsSignedIn)
                HandleSignedIn();
            else
                ShowPanel(AuthPanel.Login);
        }

        private void OnDestroy()
        {
            if (_auth == null) return;
            _auth.OnSignedIn     -= HandleSignedIn;
            _auth.OnSignInFailed -= HandleSignInFailed;
        }

        private void ShowPanel(AuthPanel panel)
        {
            _loginPanel          .SetActive(panel == AuthPanel.Login);
            _registerPanel       .SetActive(panel == AuthPanel.Register);
            if (_forgotPasswordPanel != null)
                _forgotPasswordPanel.SetActive(panel == AuthPanel.ForgotPassword);
            _loginStatusText.text = "";
            _regStatusText  .text = "";
            if (_forgotStatusText != null) _forgotStatusText.text = "";
        }

        // -------------------------------------------------------------------------
        private void HandleSignedIn()
        {
            if (_suppressAutoTransition) return;
            SetBusy(false);
            SULog.Info("Auth: signed in — advancing to game", SULog.Channel.Net);
            EventBus.Publish(new PlayerReadyEvent());
        }

        private void HandleSignInFailed(Exception ex)
        {
            SetActiveStatus(FriendlyError(ex));
            SetBusy(false);
        }

        // Cloud Code calls require an authenticated UGS session even before an
        // account exists — RequestPasswordReset/ConfirmPasswordReset fail with
        // PlayerIdMissing otherwise, since a player who forgot their password is
        // by definition signed out. Silently establishes an anonymous session if
        // none exists yet, suppressing the normal sign-in transition so the player
        // isn't dropped into the game as a guest mid-reset. Only used by the Forgot
        // Password flow (OnSendResetCodeClicked/OnResetPasswordClicked) — email
        // verification now happens post-login and doesn't need this.
        private async Task EnsureSessionAsync()
        {
            if (_auth.IsSignedIn) return;
            _suppressAutoTransition = true;
            try   { await _auth.SignInAnonymouslyAsync(); }
            finally { _suppressAutoTransition = false; }
        }

        // -------------------------------------------------------------------------
        private async void OnLoginClicked()
        {
            string email    = _loginEmailField.text.Trim();
            string password = _loginPasswordField.text;

            if (!ValidateEmail(email, out string emailErr))
            {
                _loginStatusText.text = emailErr;
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                _loginStatusText.text = "Enter your password";
                return;
            }

            SetBusy(true);
            _loginStatusText.text = "Signing in…";
            try   { await _auth.SignInWithEmailAsync(email, password); }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnGuestClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in as guest…";
            try   { await _auth.SignInAnonymouslyAsync(); }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnGoogleClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in with Google…";
            try
            {
                string idToken;
                try   { idToken = await GoogleAuthHandler.GetIdTokenAsync(); }
                catch (NotSupportedException) { idToken = "mock_google_token"; }
                await _auth.SignInWithGoogleAsync(idToken);
            }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnRegisterClicked()
        {
            string username = _regUsernameField.text.Trim();
            string email     = _regEmailField.text.Trim();
            string password  = _regPasswordField.text;
            string confirm   = _regConfirmField.text;

            if (!ValidateUsername(username, out string nameErr))
            {
                _regStatusText.text = nameErr;
                return;
            }
            if (!ValidateEmail(email, out string emailErr))
            {
                _regStatusText.text = emailErr;
                return;
            }
            if (!ValidatePassword(password, out string passErr))
            {
                _regStatusText.text = passErr;
                return;
            }
            if (password != confirm)
            {
                _regStatusText.text = "Passwords do not match";
                return;
            }

            SetBusy(true);
            _regStatusText.text = "Creating account…";
            try   { await _auth.RegisterAsync(username, password, email); }
            catch (Exception ex) { _regStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnSendResetCodeClicked()
        {
            if (_forgotEmailField == null || _forgotStatusText == null) return;
            string email = _forgotEmailField.text.Trim();
            if (!ValidateEmail(email, out string err))
            {
                _forgotStatusText.text = err;
                return;
            }

            if (_sendResetCodeButton != null) _sendResetCodeButton.interactable = false;
            _forgotStatusText.text = "Sending reset code…";
            try
            {
                await EnsureSessionAsync();
                await _auth.RequestPasswordResetAsync(email);
                _forgotStatusText.text = "Reset code sent — check your email (mock code: 123456)";
            }
            catch (Exception ex)
            {
                _forgotStatusText.text = FriendlyError(ex);
            }
            finally
            {
                if (_sendResetCodeButton != null) _sendResetCodeButton.interactable = true;
            }
        }

        private async void OnResetPasswordClicked()
        {
            if (_forgotEmailField == null || _forgotCodeField == null ||
                _forgotNewPasswordField == null || _forgotConfirmField == null ||
                _forgotStatusText == null) return;

            string email       = _forgotEmailField      .text.Trim();
            string code        = _forgotCodeField        .text.Trim();
            string newPassword = _forgotNewPasswordField .text;
            string confirm     = _forgotConfirmField     .text;

            if (!ValidateEmail(email, out string emailErr))
            {
                _forgotStatusText.text = emailErr;
                return;
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                _forgotStatusText.text = "Enter the reset code from your email";
                return;
            }
            if (!ValidatePassword(newPassword, out string passErr))
            {
                _forgotStatusText.text = passErr;
                return;
            }
            if (newPassword != confirm)
            {
                _forgotStatusText.text = "Passwords do not match";
                return;
            }

            if (_resetPasswordButton != null) _resetPasswordButton.interactable = false;
            _forgotStatusText.text = "Resetting password…";
            try
            {
                await EnsureSessionAsync();
                await _auth.ConfirmPasswordResetAsync(email, code, newPassword);
                ShowPanel(AuthPanel.Login);
                _loginStatusText.text = "Password reset — please sign in with your new password";
            }
            catch (Exception ex)
            {
                _forgotStatusText.text = FriendlyError(ex);
                if (_resetPasswordButton != null) _resetPasswordButton.interactable = true;
            }
        }

        // -------------------------------------------------------------------------
        private void SetBusy(bool busy)
        {
            _busy = busy;
            _loginButton   .interactable = !busy;
            _guestButton   .interactable = !busy;
            _registerButton.interactable = !busy;
            if (_googleButton != null) _googleButton.interactable = !busy;
        }

        private void SetActiveStatus(string message)
        {
            if (_loginPanel.activeSelf)
                _loginStatusText.text = message;
            else if (_registerPanel.activeSelf)
                _regStatusText.text = message;
            else if (_forgotPasswordPanel != null && _forgotPasswordPanel.activeSelf && _forgotStatusText != null)
                _forgotStatusText.text = message;
        }

        // UGS's real password policy: 8-30 chars, at least one uppercase, lowercase,
        // digit, and symbol. Enforced client-side so players get instant feedback
        // instead of a server rejection after submitting.
        private static bool ValidatePassword(string password, out string error)
        {
            if (password.Length < 8 || password.Length > 30)
            {
                error = "Password must be 8-30 characters";
                return false;
            }
            bool hasUpper = false, hasLower = false, hasDigit = false, hasSymbol = false;
            foreach (char c in password)
            {
                if      (char.IsUpper(c)) hasUpper  = true;
                else if (char.IsLower(c)) hasLower  = true;
                else if (char.IsDigit(c)) hasDigit  = true;
                else                      hasSymbol = true;
            }
            if (!hasUpper || !hasLower || !hasDigit || !hasSymbol)
            {
                error = "Password must include uppercase, lowercase, a number, and a symbol";
                return false;
            }
            error = null;
            return true;
        }

        private static bool ValidateEmail(string email, out string error)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                error = "Email is required";
                return false;
            }
            int at  = email.IndexOf('@');
            int dot = email.LastIndexOf('.');
            if (at < 1 || dot <= at + 1 || dot == email.Length - 1)
            {
                error = "Enter a valid email address";
                return false;
            }
            error = null;
            return true;
        }

        private static bool ValidateUsername(string username, out string error)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
            {
                error = "Username must be at least 2 characters";
                return false;
            }
            if (username.Length > 20)
            {
                error = "Username must be 20 characters or fewer";
                return false;
            }
            error = null;
            return true;
        }

        private static string FriendlyError(Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("already exists") || msg.Contains("already taken") || msg.Contains("EntityExists") || msg.Contains("ENTITY_EXISTS"))
                return "An account with that email already exists";
            if (msg.Contains("INVALID_PASSWORD") || msg.Contains("wrong password") || msg.Contains("Incorrect"))
                return "Incorrect email or password";
            if (msg.Contains("No password reset") || msg.Contains("No reset"))
                return "No reset was requested for this email — click Send Reset Code first";
            if (msg.Contains("Invalid reset code"))
                return "Incorrect reset code — check your email and try again";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
            return msg;
        }
    }
}
