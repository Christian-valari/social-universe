using UnityEngine;

namespace SocialUniverse.Config
{
    // Holds the OAuth Web Client ID Google Sign-In needs on Android — UGS's
    // SignInWithGoogleAsync verifies the device-acquired ID token against
    // this same Web client. Placeholder until the user completes the Google
    // Cloud Console / UGS dashboard setup — see
    // docs/google-signin-setup-checklist.md.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/GoogleAuthConfig", fileName = "GoogleAuthConfig")]
    public class GoogleAuthConfig : ScriptableObject
    {
        [SerializeField] private string _webClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

        public string WebClientId => _webClientId;
    }
}
