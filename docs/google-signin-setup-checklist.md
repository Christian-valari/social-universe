# Google Sign-In — Device Setup Checklist

Token acquisition uses the **Google Play Games plugin** (per the official Unity
docs: https://docs.unity.com/en-us/authentication/platform-signin/google).
These are the plugin-import + account/console steps only a project owner can
do. Nothing works on a real Android device until all of them are done. See also
`docs/superpowers/specs/2026-07-23-google-signin-play-games-plugin-design.md`.

1. **Import the Google Play Games plugin (v0.10.14)** into the Unity Editor:
   - Download the `.unitypackage` from
     https://github.com/playgameservices/play-games-plugin-for-unity/releases/tag/v10.14
   - Assets → Import Package → Custom Package… → import all.
   - This is required before the Android target will compile — until then the
     `GooglePlayGames.*` calls in `GoogleAuthHandler` fail with `CS0246`
     (Phase B in the design doc). After import, add the plugin's assembly
     (e.g. `GooglePlayGames`) to `SocialUniverse.Net.asmdef`'s references.

2. **Google Cloud Console** (https://console.cloud.google.com/):
   - Configure the OAuth consent screen for the project.
   - Create OAuth 2.0 credentials → **Web application** client ID. Copy it —
     this is the value that goes in steps 4 and 5.
   - Create OAuth 2.0 credentials → **Android** client ID:
     - Package name: `com.ValariSolutions.socialuniverse` (from
       `ProjectSettings/AndroidResolverDependencies.xml`'s `bundleId`).
     - SHA-1 fingerprint of the signing keystore:
       ```
       keytool -list -v -keystore zKeystore/user.keystore -alias <alias> -storepass <password>
       ```
       Copy the `SHA1:` fingerprint line into the Android client's config.

3. **Google Play Console → Play Games Services**:
   - Create / configure the Play Games Services project for this game.
   - Link the **Android** OAuth client from step 2 (same package name + SHA-1).
   - Add your Google account(s) as **license testers** so sign-in works before
     the game is published.
   - In Unity: **Window → Google Play Games → Setup → Android setup…**, paste
     the **Web** client ID from step 2 (the plugin bakes it into its generated
     Android manifest — `RequestIdToken()` uses it to mint the ID token).

4. **Unity Gaming Services dashboard**: Authentication → enable **Google**
   as an identity provider, pasting in the **Web** client ID from step 2
   (not the Android one — UGS validates ID tokens against the Web client).

5. **This repo**: open `Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset`
   in the Inspector and replace the placeholder `_webClientId` with the same
   Web client ID from step 2. (This value is also the "setup not done yet"
   tripwire in `GoogleAuthHandler` — sign-in refuses to start while it's the
   placeholder.)

6. **Device smoke test** (after 1-5 are done and a Development Build is
   installed on an Android device signed with the same keystore as step 2):
   - First Google sign-in → the choose-name panel appears → enter a name →
     enters the game.
   - Reinstall (or clear app data) and sign in again with the same Google
     account → goes straight into the game, no name panel.
