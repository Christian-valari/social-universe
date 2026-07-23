using UnityEngine;

namespace SocialUniverse.Config
{
    // Holds the OAuth Web Client ID for Google Play Games (v2) sign-in. UGS
    // exchanges the device-acquired server auth code against this Web client
    // (ID + secret, configured in the UGS Authentication dashboard). Placeholder
    // until the user completes the Google Cloud Console / UGS dashboard setup —
    // see docs/google-signin-setup-checklist.md.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/GoogleAuthConfig", fileName = "GoogleAuthConfig")]
    public class GoogleAuthConfig : ScriptableObject
    {
        [SerializeField] private string _webClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

        public string WebClientId => _webClientId;
    }
}
