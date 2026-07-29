using UnityEngine;

namespace SocialUniverse.Config
{
    // Firebase project identity used to bridge Firebase Auth into UGS via the
    // custom `oidc-firebase` OpenID Connect provider. ProjectId is the OIDC
    // issuer audience (https://securetoken.google.com/<ProjectId>);
    // GoogleWebClientId is the OAuth web client for the Google provider.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/FirebaseAuthConfig", fileName = "FirebaseAuthConfig")]
    public class FirebaseAuthConfig : ScriptableObject
    {
        [SerializeField] private string _projectId = "YOUR_FIREBASE_PROJECT_ID";
        [SerializeField] private string _googleWebClientId = "YOUR_WEB_CLIENT_ID.apps.googleusercontent.com";

        public string ProjectId         => _projectId;
        public string GoogleWebClientId => _googleWebClientId;
    }
}
