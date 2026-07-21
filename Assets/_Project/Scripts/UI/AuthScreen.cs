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
        private enum AuthPanel { Login, Register, ForgotPasswordEmail, ForgotPasswordReset, VerifyEmail, ChooseName }

        // --- Panels ---
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;
        [SerializeField] private GameObject _forgotEmailPanel;
        [SerializeField] private GameObject _forgotResetPanel;
        [SerializeField] private GameObject _verifyEmailPanel;
        [SerializeField] private GameObject _chooseNamePanel;

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

        // --- Forgot password: email panel ---
        [SerializeField] private InputField _forgotEmailField;
        [SerializeField] private Text       _forgotEmailStatusText;
        [SerializeField] private Button     _sendResetCodeButton;
        [SerializeField] private Button     _forgotBackToLoginButton;

        // --- Forgot password: reset panel ---
        [SerializeField] private InputField _forgotCodeField;
        [SerializeField] private InputField _forgotNewPasswordField;
        [SerializeField] private InputField _forgotConfirmField;
        [SerializeField] private Text       _forgotResetStatusText;
        [SerializeField] private Button     _resetPasswordButton;
        [SerializeField] private Button     _forgotResetBackButton;

        // --- Verify email panel ---
        [SerializeField] private InputField _verifyCodeField;
        [SerializeField] private Text       _verifyStatusText;
        [SerializeField] private Button     _verifyButton;
        [SerializeField] private Button     _resendCodeButton;
        [SerializeField] private Button     _verifyCancelButton;

        // --- Choose display name panel (first Google sign-in) ---
        [SerializeField] private InputField _chooseNameInput;
        [SerializeField] private Button     _chooseNameConfirmButton;
        [SerializeField] private Text       _chooseNameStatusText;

        private IAuthService _auth;
        private bool         _busy;
        private bool         _suppressAutoTransition;
        private bool         _pendingVerification; // account created, email not yet verified
        private string       _pendingResetEmail;   // captured on the send-code panel for the reset call

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
            if (_forgotResetBackButton   != null) _forgotResetBackButton  .onClick.AddListener(() => ShowPanel(AuthPanel.ForgotPasswordEmail));
            if (_sendResetCodeButton     != null) _sendResetCodeButton    .onClick.AddListener(OnSendResetCodeClicked);
            if (_resetPasswordButton     != null) _resetPasswordButton    .onClick.AddListener(OnResetPasswordClicked);
            if (_verifyButton            != null) _verifyButton           .onClick.AddListener(OnVerifyClicked);
            if (_resendCodeButton        != null) _resendCodeButton       .onClick.AddListener(OnResendCodeClicked);
            if (_verifyCancelButton      != null) _verifyCancelButton     .onClick.AddListener(OnVerifyCancelClicked);
            if (_chooseNameConfirmButton != null) _chooseNameConfirmButton.onClick.AddListener(OnChooseNameConfirmed);

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

        // Desktop/editor quits: drop any session that must not survive into the
        // next launch — the throwaway anonymous Cloud Code session, or a freshly
        // registered account whose email was never verified. Mobile swipe-kills
        // skip this callback entirely; BootState's IsAnonymous guard is the
        // safety net there.
        private void OnApplicationQuit()
        {
            if (_auth == null || !_auth.IsSignedIn) return;
            if (_auth.IsAnonymous || _pendingVerification)
                _ = _auth.SignOutAsync();
        }

        private void ShowPanel(AuthPanel panel)
        {
            _loginPanel   .SetActive(panel == AuthPanel.Login);
            _registerPanel.SetActive(panel == AuthPanel.Register);
            if (_forgotEmailPanel != null) _forgotEmailPanel.SetActive(panel == AuthPanel.ForgotPasswordEmail);
            if (_forgotResetPanel != null) _forgotResetPanel.SetActive(panel == AuthPanel.ForgotPasswordReset);
            if (_verifyEmailPanel != null) _verifyEmailPanel.SetActive(panel == AuthPanel.VerifyEmail);
            if (_chooseNamePanel  != null) _chooseNamePanel .SetActive(panel == AuthPanel.ChooseName);

            _loginStatusText.text = "";
            _regStatusText  .text = "";
            if (_forgotEmailStatusText != null) _forgotEmailStatusText.text = "";
            if (_forgotResetStatusText != null) _forgotResetStatusText.text = "";
            if (_verifyStatusText      != null) _verifyStatusText     .text = "";
            if (_chooseNameStatusText  != null) _chooseNameStatusText .text = "";

            // Leaving a flow for the Login panel ends any in-flight registration
            // suppression; the flows themselves manage the flag while active.
            if (panel == AuthPanel.Login)
                _suppressAutoTransition = false;
        }

        // -------------------------------------------------------------------------
        private void HandleSignedIn()
        {
            if (_suppressAutoTransition) return;
            // Anonymous sessions are a Cloud Code transport, never a player:
            // guest play was removed, so nothing anonymous may enter the game.
            if (_auth.IsAnonymous)
            {
                SULog.Warn("Auth: anonymous session may not enter the game — ignoring sign-in", SULog.Channel.Net);
                return;
            }
            // A restored SSO session with no display name yet (the app was
            // quit while gated at the choose-name panel — see BootState) is
            // shown the panel again instead of entering the game nameless.
            if (string.IsNullOrEmpty(_auth.DisplayName))
            {
                SetBusy(false);
                ShowPanel(AuthPanel.ChooseName);
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

        // Cloud Code calls require an authenticated UGS session even before an
        // account exists — CheckEmailAvailable and the password-reset functions
        // fail with PlayerIdMissing otherwise. Silently establishes an anonymous
        // session if none exists yet. Preserves the caller's suppression state
        // instead of clobbering it: registration holds the flag across this call.
        private async Task EnsureSessionAsync()
        {
            if (_auth.IsSignedIn) return;
            bool prev = _suppressAutoTransition;
            _suppressAutoTransition = true;
            try   { await _auth.SignInAnonymouslyAsync(); }
            finally { _suppressAutoTransition = prev; }
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
                // A registration pre-check or forgot-password flow may have left
                // a throwaway anonymous transport session alive; UGS's
                // SignInWithUsernamePasswordAsync throws "already signed in" over
                // it. Drop it before signing in for real.
                if (_auth.IsSignedIn && _auth.IsAnonymous) await _auth.SignOutAsync();
                await _auth.SignInWithEmailAsync(email, password);
            }
            catch (Exception ex) { _loginStatusText.text = FriendlyError(ex); SetBusy(false); }
        }

        private async void OnGoogleClicked()
        {
            SetBusy(true);
            _loginStatusText.text = "Signing in with Google…";
            // Suppress HandleSignedIn's auto-publish: the SignedIn event can
            // fire before this method gets a chance to check below whether
            // this is a first-time sign-in — same mechanism the verify-email
            // flow uses. Cleared explicitly once we know which path to take.
            _suppressAutoTransition = true;
            try
            {
                // Same anonymous-transport cleanup as OnLoginClicked: UGS rejects
                // an SSO sign-in over a live anonymous session with "already
                // signed in".
                if (_auth.IsSignedIn && _auth.IsAnonymous) await _auth.SignOutAsync();
                string idToken;
                try   { idToken = await GoogleAuthHandler.GetIdTokenAsync(); }
                catch (NotSupportedException) { idToken = "mock_google_token"; }
                await _auth.SignInWithGoogleAsync(idToken);

                if (string.IsNullOrEmpty(_auth.DisplayName))
                {
                    // First Google sign-in: hold at Auth until a name is chosen.
                    SetBusy(false);
                    ShowPanel(AuthPanel.ChooseName);
                }
                else
                {
                    _suppressAutoTransition = false;
                    SetBusy(false);
                    SULog.Info("Auth: Google sign-in (returning player) — advancing to game", SULog.Channel.Net);
                    EventBus.Publish(new PlayerReadyEvent());
                }
            }
            catch (Exception ex)
            {
                _suppressAutoTransition = false;
                _loginStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
        }

        private async void OnChooseNameConfirmed()
        {
            string name = _chooseNameInput.text;
            if (!DisplayNameValidator.Validate(name, out string err))
            {
                _chooseNameStatusText.text = err;
                return;
            }

            SetBusy(true);
            _chooseNameStatusText.text = "Saving…";
            try
            {
                await _auth.UpdateDisplayNameAsync(name.Trim());
                _suppressAutoTransition = false;
                SetBusy(false);
                SULog.Info("Auth: display name chosen — advancing to game", SULog.Channel.Net);
                EventBus.Publish(new PlayerReadyEvent());
            }
            catch (Exception ex)
            {
                _chooseNameStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
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
            // Raised for the whole registration flow: the anonymous pre-check
            // session and the account upgrade both fire sign-in signals that
            // must not advance to the game — only a verified email does (see
            // OnVerifyClicked). Cleared on verify success, cancel, or returning
            // to the Login panel.
            _suppressAutoTransition = true;
            try
            {
                _regStatusText.text = "Checking email…";
                await EnsureSessionAsync();
                if (!await _auth.IsEmailAvailableAsync(email))
                {
                    _regStatusText.text = "An account with that email already exists";
                    SetBusy(false);
                    return;
                }

                _regStatusText.text = "Creating account…";
                await _auth.RegisterAsync(username, password, email);
                _pendingVerification = true;

                ShowPanel(AuthPanel.VerifyEmail);
                await SendVerificationCodeAsync();
            }
            catch (Exception ex)
            {
                // If the UGS account was already created (AddUsernamePassword/
                // SignUpWithUsernamePassword succeeded) but a later awaited step
                // threw, the session is live and non-anonymous. Staying on the
                // Register panel would strand it: a retry hits "already signed
                // in" and so does Login. Advance to the Verify panel instead —
                // Resend covers a code that was never sent, Cancel deletes the
                // account.
                if (_auth.IsSignedIn && !_auth.IsAnonymous)
                {
                    _pendingVerification = true;
                    ShowPanel(AuthPanel.VerifyEmail);
                    _verifyStatusText.text = FriendlyError(ex);
                    SetBusy(false);
                }
                else
                {
                    _regStatusText.text = FriendlyError(ex);
                    SetBusy(false);
                }
            }
        }

        // -------------------------------------------------------------------------
        private async Task SendVerificationCodeAsync()
        {
            SetBusy(true);
            _verifyStatusText.text = "Sending verification code…";
            try
            {
                await _auth.RequestEmailVerificationCodeAsync();
                _verifyStatusText.text = "Verification code sent — check your email";
            }
            catch (Exception ex)
            {
                _verifyStatusText.text = FriendlyError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void OnResendCodeClicked() => await SendVerificationCodeAsync();

        private async void OnVerifyClicked()
        {
            string code = _verifyCodeField.text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                _verifyStatusText.text = "Enter the verification code sent to your email";
                return;
            }

            SetBusy(true);
            _verifyStatusText.text = "Verifying…";
            try
            {
                await _auth.ConfirmEmailVerificationCodeAsync(code);
                _pendingVerification    = false;
                _suppressAutoTransition = false;
                SetBusy(false);
                SULog.Info("Auth: email verified — advancing to game", SULog.Channel.Net);
                EventBus.Publish(new PlayerReadyEvent());
            }
            catch (Exception ex)
            {
                _verifyStatusText.text = FriendlyError(ex);
                SetBusy(false);
            }
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
            _pendingVerification = false;
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
            _forgotEmailStatusText.text = "Sending reset code…";
            try
            {
                await EnsureSessionAsync();
                await _auth.RequestPasswordResetAsync(email);
                _pendingResetEmail = email;
                ShowPanel(AuthPanel.ForgotPasswordReset);
                _forgotResetStatusText.text = "Reset code sent — check your email";
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

        private async void OnResetPasswordClicked()
        {
            string code        = _forgotCodeField.text.Trim();
            string newPassword = _forgotNewPasswordField.text;
            string confirm     = _forgotConfirmField.text;

            if (string.IsNullOrWhiteSpace(code))
            {
                _forgotResetStatusText.text = "Enter the reset code from your email";
                return;
            }
            if (!ValidatePassword(newPassword, out string passErr))
            {
                _forgotResetStatusText.text = passErr;
                return;
            }
            if (newPassword != confirm)
            {
                _forgotResetStatusText.text = "Passwords do not match";
                return;
            }

            SetBusy(true);
            _forgotResetStatusText.text = "Resetting password…";
            try
            {
                await EnsureSessionAsync();
                await _auth.ConfirmPasswordResetAsync(_pendingResetEmail, code, newPassword);
                // The reset ran on a throwaway anonymous session — drop it now,
                // or the upcoming email login throws UGS's "already signed in".
                if (_auth.IsAnonymous)
                    await _auth.SignOutAsync();
                ShowPanel(AuthPanel.Login);
                _loginStatusText.text = "Password reset — please sign in with your new password";
            }
            catch (Exception ex)
            {
                _forgotResetStatusText.text = FriendlyError(ex);
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
            _loginButton       .interactable = !busy;
            _registerButton    .interactable = !busy;
            if (_googleButton            != null) _googleButton           .interactable = !busy;
            if (_goToRegisterButton      != null) _goToRegisterButton     .interactable = !busy;
            if (_goToLoginButton         != null) _goToLoginButton        .interactable = !busy;
            if (_forgotPasswordButton    != null) _forgotPasswordButton   .interactable = !busy;
            if (_forgotBackToLoginButton != null) _forgotBackToLoginButton.interactable = !busy;
            if (_forgotResetBackButton   != null) _forgotResetBackButton  .interactable = !busy;
            if (_sendResetCodeButton     != null) _sendResetCodeButton    .interactable = !busy;
            if (_resetPasswordButton     != null) _resetPasswordButton    .interactable = !busy;
            if (_verifyButton            != null) _verifyButton           .interactable = !busy;
            if (_resendCodeButton        != null) _resendCodeButton       .interactable = !busy;
            if (_verifyCancelButton      != null) _verifyCancelButton     .interactable = !busy;
            if (_chooseNameConfirmButton != null) _chooseNameConfirmButton.interactable = !busy;
        }

        private void SetActiveStatus(string message)
        {
            if (_loginPanel.activeSelf)
                _loginStatusText.text = message;
            else if (_registerPanel.activeSelf)
                _regStatusText.text = message;
            else if (_forgotEmailPanel != null && _forgotEmailPanel.activeSelf && _forgotEmailStatusText != null)
                _forgotEmailStatusText.text = message;
            else if (_forgotResetPanel != null && _forgotResetPanel.activeSelf && _forgotResetStatusText != null)
                _forgotResetStatusText.text = message;
            else if (_verifyEmailPanel != null && _verifyEmailPanel.activeSelf && _verifyStatusText != null)
                _verifyStatusText.text = message;
            else if (_chooseNamePanel != null && _chooseNamePanel.activeSelf && _chooseNameStatusText != null)
                _chooseNameStatusText.text = message;
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
            if (msg.Contains("No email on file"))
                return "No email is on file for this account — contact support";
            if (msg.Contains("No verification code"))
                return "No verification code was sent — click Resend Code";
            if (msg.Contains("Verification code has expired"))
                return "Verification code expired — click Resend Code to get a new one";
            if (msg.Contains("Invalid verification code"))
                return "Incorrect verification code — check your email and try again";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
            return msg;
        }
    }
}
