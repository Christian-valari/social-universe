using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;

namespace SocialUniverse.UI
{
    public class AuthScreen : MonoBehaviour
    {
        private enum AuthPanel { Login, Register, ForgotPasswordEmail, VerifyEmail }

        // --- Panels ---
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;
        [SerializeField] private GameObject _forgotEmailPanel;
        [SerializeField] private GameObject _verifyEmailPanel;

        // --- Login panel ---
        [SerializeField] private InputField _loginEmailField;
        [SerializeField] private InputField _loginPasswordField;
        [SerializeField] private Text       _loginStatusText;
        [SerializeField] private Button     _loginButton;
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

        // --- Forgot password: email panel (single-step Firebase reset link) ---
        [SerializeField] private InputField _forgotEmailField;
        [SerializeField] private Text       _forgotEmailStatusText;
        [SerializeField] private Button     _sendResetCodeButton;
        [SerializeField] private Button     _forgotBackToLoginButton;

        // --- Verify email panel (link-based Firebase verification) ---
        [SerializeField] private Text   _verifyStatusText;
        [SerializeField] private Button _verifyButton;      // "I've verified my email"
        [SerializeField] private Button _resendCodeButton;  // "Resend email"
        [SerializeField] private Button _verifyCancelButton;

        private IAuthService _auth;
        private bool         _busy;

        [Inject]
        public void Construct(IAuthService auth) => _auth = auth;

        private void Start()
        {
            _auth.OnSignedIn     += HandleSignedIn;
            _auth.OnSignInFailed += HandleSignInFailed;

            _loginButton       .onClick.AddListener(OnLoginClicked);
            _goToRegisterButton.onClick.AddListener(() => ShowPanel(AuthPanel.Register));
            _registerButton    .onClick.AddListener(OnRegisterClicked);
            _goToLoginButton   .onClick.AddListener(() => ShowPanel(AuthPanel.Login));

            if (_googleButton            != null) _googleButton           .onClick.AddListener(OnGoogleClicked);
            if (_forgotPasswordButton    != null) _forgotPasswordButton   .onClick.AddListener(() => ShowPanel(AuthPanel.ForgotPasswordEmail));
            if (_forgotBackToLoginButton != null) _forgotBackToLoginButton.onClick.AddListener(() => ShowPanel(AuthPanel.Login));
            if (_sendResetCodeButton     != null) _sendResetCodeButton    .onClick.AddListener(OnSendResetCodeClicked);
            if (_verifyButton            != null) _verifyButton           .onClick.AddListener(OnVerifyClicked);
            if (_resendCodeButton        != null) _resendCodeButton       .onClick.AddListener(OnResendCodeClicked);
            if (_verifyCancelButton      != null) _verifyCancelButton     .onClick.AddListener(OnVerifyCancelClicked);

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

        // Desktop/editor quits: drop a freshly-registered account whose email was
        // never verified so it can't be resumed into the game on next launch.
        // Mobile swipe-kills skip this callback; BootState's IsEmailVerified gate
        // is the safety net there.
        private void OnApplicationQuit()
        {
            if (_auth == null || !_auth.IsSignedIn) return;
            if (!_auth.IsEmailVerified)
                _ = _auth.SignOutAsync();
        }

        private void ShowPanel(AuthPanel panel)
        {
            _loginPanel   .SetActive(panel == AuthPanel.Login);
            _registerPanel.SetActive(panel == AuthPanel.Register);
            if (_forgotEmailPanel != null) _forgotEmailPanel.SetActive(panel == AuthPanel.ForgotPasswordEmail);
            if (_verifyEmailPanel != null) _verifyEmailPanel.SetActive(panel == AuthPanel.VerifyEmail);

            _loginStatusText.text = "";
            _regStatusText  .text = "";
            if (_forgotEmailStatusText != null) _forgotEmailStatusText.text = "";
            if (_verifyStatusText      != null) _verifyStatusText     .text = "";
        }

        // -------------------------------------------------------------------------
        private void HandleSignedIn()
        {
            // Every UGS sign-in (login, register's OIDC bridge, Google) raises this.
            // An unverified email account must never enter the game — the Verify
            // panel drives it to completion. Google accounts report a verified
            // email and advance immediately.
            if (!_auth.IsEmailVerified)
            {
                SULog.Info("Auth: signed in but email unverified — awaiting verification", SULog.Channel.Net);
                ShowPanel(AuthPanel.VerifyEmail);
                _verifyStatusText.text = "Please verify your email — check your inbox for the link, then tap ‘I've verified’.";
                SetBusy(false);
                return;
            }
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
            try
            {
                await _auth.SignInWithEmailAsync(email, password);
                // Verified accounts advance via HandleSignedIn; an unverified
                // account signed in here must finish verification first.
                if (!_auth.IsEmailVerified)
                {
                    ShowPanel(AuthPanel.VerifyEmail);
                    _verifyStatusText.text = "Your email isn't verified yet — check your inbox or resend the link";
                    SetBusy(false);
                }
            }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnGoogleClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in with Google…";
            try { await _auth.SignInWithGoogleAsync(); }
            catch (NotSupportedException) { _loginStatusText.text = "Google sign-in isn't available here"; SetBusy(false); }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnRegisterClicked()
        {
            string username = _regUsernameField.text.Trim();
            string email    = _regEmailField.text.Trim();
            string password = _regPasswordField.text;
            string confirm  = _regConfirmField.text;

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
            try
            {
                // AuthService.RegisterAsync creates the Firebase account, bridges
                // to UGS, and sends the verification email. The sign-in it triggers
                // is gated in HandleSignedIn (unverified accounts don't advance).
                await _auth.RegisterAsync(username, password, email);
                ShowPanel(AuthPanel.VerifyEmail);
                _verifyStatusText.text = "Account created — check your email and click the verification link";
                SetBusy(false);
            }
            catch (Exception ex)
            {
                _regStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
        }

        // -------------------------------------------------------------------------
        private async void OnVerifyClicked() // button relabelled "I've verified my email"
        {
            SetBusy(true);
            _verifyStatusText.text = "Checking…";
            try
            {
                if (await _auth.ReloadAndCheckVerifiedAsync())
                {
                    SetBusy(false);
                    SULog.Info("Auth: email verified — advancing to game", SULog.Channel.Net);
                    EventBus.Publish(new PlayerReadyEvent());
                }
                else
                {
                    _verifyStatusText.text = "Not verified yet — click the link in your email, then try again";
                    SetBusy(false);
                }
            }
            catch (Exception ex) { _verifyStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnResendCodeClicked() // "Resend email"
        {
            SetBusy(true);
            try { await _auth.SendEmailVerificationAsync(); _verifyStatusText.text = "Verification email sent"; }
            catch (Exception ex) { _verifyStatusText.text = FriendlyError(ex); }
            finally { SetBusy(false); }
        }

        private async void OnVerifyCancelClicked()
        {
            SetBusy(true);
            _verifyStatusText.text = "Removing unverified account…";
            try
            {
                await _auth.DeleteAccountAsync();
            }
            catch (Exception ex)
            {
                // Deletion needs a live session; if it fails, at least drop the
                // session so the unverified account can't be resumed into the
                // game on the next launch.
                SULog.Warn($"Auth: account deletion failed ({ex.Message}) — signing out instead", SULog.Channel.Net);
                try { await _auth.SignOutAsync(); } catch { /* best effort */ }
            }
            SetBusy(false);
            ShowPanel(AuthPanel.Login);
            _loginStatusText.text = "Registration cancelled — the unverified account was removed";
        }

        // -------------------------------------------------------------------------
        private async void OnSendResetCodeClicked()
        {
            string email = _forgotEmailField.text.Trim();
            if (!ValidateEmail(email, out string err))
            {
                _forgotEmailStatusText.text = err;
                return;
            }

            SetBusy(true);
            _forgotEmailStatusText.text = "Sending reset link…";
            try
            {
                await _auth.RequestPasswordResetAsync(email);
                ShowPanel(AuthPanel.Login);
                _loginStatusText.text = "Password reset link sent — check your email";
            }
            catch (Exception ex)
            {
                _forgotEmailStatusText.text = FriendlyError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // -------------------------------------------------------------------------
        private void SetBusy(bool busy)
        {
            _busy = busy;
            _loginButton   .interactable = !busy;
            _registerButton.interactable = !busy;
            if (_googleButton            != null) _googleButton           .interactable = !busy;
            if (_goToRegisterButton      != null) _goToRegisterButton     .interactable = !busy;
            if (_goToLoginButton         != null) _goToLoginButton        .interactable = !busy;
            if (_forgotPasswordButton    != null) _forgotPasswordButton   .interactable = !busy;
            if (_forgotBackToLoginButton != null) _forgotBackToLoginButton.interactable = !busy;
            if (_sendResetCodeButton     != null) _sendResetCodeButton    .interactable = !busy;
            if (_verifyButton            != null) _verifyButton           .interactable = !busy;
            if (_resendCodeButton        != null) _resendCodeButton       .interactable = !busy;
            if (_verifyCancelButton      != null) _verifyCancelButton     .interactable = !busy;
        }

        private void SetActiveStatus(string message)
        {
            if (_loginPanel.activeSelf)
                _loginStatusText.text = message;
            else if (_registerPanel.activeSelf)
                _regStatusText.text = message;
            else if (_forgotEmailPanel != null && _forgotEmailPanel.activeSelf && _forgotEmailStatusText != null)
                _forgotEmailStatusText.text = message;
            else if (_verifyEmailPanel != null && _verifyEmailPanel.activeSelf && _verifyStatusText != null)
                _verifyStatusText.text = message;
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
            if (msg.Contains("already exists") || msg.Contains("already taken") || msg.Contains("EntityExists") || msg.Contains("ENTITY_EXISTS") || msg.Contains("EMAIL_EXISTS"))
                return "An account with that email already exists";
            if (msg.Contains("INVALID_PASSWORD") || msg.Contains("wrong password") || msg.Contains("Incorrect") || msg.Contains("INVALID_LOGIN_CREDENTIALS"))
                return "Incorrect email or password";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
            return msg;
        }
    }
}
