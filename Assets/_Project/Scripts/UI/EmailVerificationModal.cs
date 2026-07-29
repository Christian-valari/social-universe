using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Progression;
using SocialUniverse.Safety;
using SocialUniverse.Config;

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
        [Inject] private IAudioManager _audio;

        private void Awake()
        {
            _sendCodeButton.onClick.AddListener(OnSendCodeClicked);
            _verifyButton  .onClick.AddListener(OnVerifyClicked);
            _closeButton   .onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            _statusText.text = "";
            bool verified = _playerState.IsEmailVerified;
            _sendCodeButton.gameObject.SetActive(!verified);
            _verifyButton  .gameObject.SetActive(!verified);
            // Firebase verification is link-based; the OTP code entry is retired.
            // The serialized field is kept for prefab stability but always hidden.
            if (_codeInput != null) _codeInput.gameObject.SetActive(false);
            _statusText.text = verified ? "Your email is verified." : "";
            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
        }

        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
        }

        private async void OnSendCodeClicked()
        {
            SetBusy(true);
            _statusText.text = "Sending verification email…";
            try
            {
                await _auth.SendEmailVerificationAsync();
                _statusText.text = "Verification email sent — check your inbox and click the link";
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
            SetBusy(true);
            _statusText.text = "Checking…";
            try
            {
                if (await _auth.ReloadAndCheckVerifiedAsync())
                {
                    _playerState.SetEmailVerified(true);
                    _statusText.text = "Your email is verified.";
                    _sendCodeButton.gameObject.SetActive(false);
                    _verifyButton  .gameObject.SetActive(false);
                    if (_codeInput != null) _codeInput.gameObject.SetActive(false);
                }
                else
                {
                    _statusText.text = "Not verified yet — click the link in your email, then try again";
                }
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
