# Google Sign-In — Device Setup Checklist

Code-complete; these are the account/console steps only a project owner can
do. Nothing in the app works on a real Android device until all four are
done.

1. **Google Cloud Console** (https://console.cloud.google.com/):
   - Configure the OAuth consent screen for the project.
   - Create OAuth 2.0 credentials → **Web application** client ID. Copy it —
     this is the value that goes in step 3.
   - Create OAuth 2.0 credentials → **Android** client ID:
     - Package name: `com.ValariSolutions.socialuniverse` (from
       `ProjectSettings/AndroidResolverDependencies.xml`'s `bundleId`).
     - SHA-1 fingerprint of the signing keystore:
       ```
       keytool -list -v -keystore zKeystore/user.keystore -alias <alias> -storepass <password>
       ```
       Copy the `SHA1:` fingerprint line into the Android client's config.

2. **Unity Gaming Services dashboard**: Authentication → enable **Google**
   as an identity provider, pasting in the **Web** client ID from step 1
   (not the Android one — UGS validates ID tokens against the Web client).

3. **This repo**: open `Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset`
   in the Inspector and replace the placeholder `_webClientId` with the same
   Web client ID from step 1.

4. **Device smoke test** (after 1-3 are done and a Development Build is
   installed on an Android device signed with the same keystore as step 1):
   - First Google sign-in → the choose-name panel appears → enter a name →
     enters the game.
   - Reinstall (or clear app data) and sign in again with the same Google
     account → goes straight into the game, no name panel.
