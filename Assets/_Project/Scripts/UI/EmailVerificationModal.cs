using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Progression;

namespace SocialUniverse.UI
{
    // Pop-up modal for post-login email verification. Auto-opened once by
    // HUDController (see ShowEmailVerificationPromptEvent) after first login if
    // the player hasn't verified yet; also reachable any time via the HUD's
    // verify-email button. Mirrors DisplayNameModal's structure.
    public class EmailVerificationModal : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _codeInput;
        [SerializeField] private Button         _sendCodeButton;
        [SerializeField] private Button         _verifyButton;
        [SerializeField] private Button         _closeButton;
        [SerializeField] private TMP_Text       _statusText;

        [Inject] private IAuthService _auth;
        [Inject] private PlayerState  _playerState;

        private void Awake()
        {
            _sendCodeButton.onClick.AddListener(OnSendCodeClicked);
            _verifyButton  .onClick.AddListener(OnVerifyClicked);
            _closeButton   .onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            _codeInput.text  = "";
            _statusText.text = "";
            bool verified = _playerState.IsEmailVerified;
            _sendCodeButton.gameObject.SetActive(!verified);
            _verifyButton  .gameObject.SetActive(!verified);
            _codeInput     .gameObject.SetActive(!verified);
            _statusText.text = verified ? "Your email is verified." : "";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private async void OnSendCodeClicked()
        {
            SetBusy(true);
            _statusText.text = "Sending verification code…";
            try
            {
                await _auth.RequestEmailVerificationCodeAsync();
                _statusText.text = "Verification code sent — check your email (mock code: 123456)";
            }
            catch (Exception ex)
            {
                _statusText.text = FriendlyError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void OnVerifyClicked()
        {
            string code = _codeInput.text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                _statusText.text = "Enter the verification code sent to your email";
                return;
            }

            SetBusy(true);
            _statusText.text = "Verifying…";
            try
            {
                await _auth.ConfirmEmailVerificationCodeAsync(code);
                _playerState.SetEmailVerified(true);
                _statusText.text = "Your email is verified.";
                _sendCodeButton.gameObject.SetActive(false);
                _verifyButton  .gameObject.SetActive(false);
                _codeInput     .gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                _statusText.text = FriendlyError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _sendCodeButton.interactable = !busy;
            _verifyButton  .interactable = !busy;
        }

        private static string FriendlyError(Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("No email on file"))
                return "No email is on file for this account — contact support";
            if (msg.Contains("Please wait a moment"))
                return "Please wait a moment before requesting another code";
            if (msg.Contains("No verification code"))
                return "No verification code was sent — click Send Code first";
            if (msg.Contains("Verification code has expired"))
                return "Verification code expired — click Send Code to get a new one";
            if (msg.Contains("Invalid verification code"))
                return "Incorrect verification code — check your email and try again";
            if (msg.Contains("network") || msg.Contains("Network") || msg.Contains("unreachable"))
                return "Network error — check your connection";
            return msg;
        }
    }
}
