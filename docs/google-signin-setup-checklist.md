# Google Sign-In — Device Setup Checklist (Play Games Services **v2**)

Token acquisition uses the **Google Play Games plugin (v2 / Play Games Services
v2)** — Google **blocks the v1 SDK (`com.google.android.gms:play-services-games`)
at upload**, so v1 is not an option. Per Unity's docs:
https://docs.unity.com/en-us/authentication/platform-signin/google-play-games
Nothing works on a real Android device until all steps are done. See also
`docs/superpowers/specs/2026-07-23-google-signin-play-games-plugin-design.md`.

**v2 flow in one line:** plugin `Authenticate()` → `RequestServerSideAccess()`
returns a one-time **server auth code** → `IAuthService.SignInWithGoogleAsync(authCode)`
→ UGS `SignInWithGooglePlayGamesAsync` exchanges it server-side (needs Web client
ID **and secret**). No ID token, unlike v1.

1. **Import the Google Play Games plugin v11.01+ (v2).** The old v0.10.14 (v1)
   plugin has been REMOVED on this branch.
   - Import from the `.tar.gz`/`.unitypackage` you downloaded
     (`current-build/…v2 unitypackage`).
   - **UNCHECK the `Assets/ExternalDependencyManager` folder on import** — this
     project already has a newer EDM4U UPM package
     (`com.google.external-dependency-manager` 1.2.187); importing the bundled
     copy duplicates its DLLs and throws import errors.
   - After import, add the plugin's runtime assembly (e.g. `GooglePlayGames`) to
     `SocialUniverse.Net.asmdef`'s references so `GoogleAuthHandler` compiles for
     Android. Then **Assets → External Dependency Manager → Android Resolver →
     Force Resolve**.
   - NOTE: `.aar`/`.srcaar`/`.jar` are marked `binary` in `.gitattributes` (the
     original `.aar`-corruption fix). Don't remove those rules.

2. **Google Cloud Console** (https://console.cloud.google.com/):
   - Configure the OAuth consent screen; add your test accounts under **Test users**.
   - Create OAuth 2.0 credentials → **Web application** client ID. Copy **both the
     client ID and the client secret** — v2/UGS needs both (step 4).
   - Create OAuth 2.0 credentials → **Android** client ID:
     - Package name: `com.ValariSolutions.socialuniverse`.
     - SHA-1 of the keystore that ACTUALLY signs the installed build. A Unity
       dev build with no keystore set in Player Settings uses the **debug
       keystore** — verify with the installed APK, not by assumption:
       ```
       adb shell pm path com.ValariSolutions.socialuniverse
       adb pull <printed path> app.apk
       keytool -printcert -jarfile app.apk
       ```
       (Mismatched SHA-1 is the #1 cause of sign-in returning `Canceled` right
       after the account picker.)

3. **Google Play Console → Play Games Services** (before the Unity Android setup):
   - Setup and management → **Configuration**: create/configure the game (mints
     the **App ID** / project ID).
   - Link the **Android** OAuth client from step 2 (same package + SHA-1).
   - Add your Google account(s) under **Testers** (unpublished games reject
     non-testers → `Canceled`).
   - Use **"Get resources"** (Android) and copy the `<resources>…</resources>`
     XML — needed by the Unity setup dialog below.

3b. **Unity → Window → Google Play Games → Setup → Android setup…**:
   - **Constants class name:** `GPGSIds` (must not be blank).
   - **Resources Definition:** paste the **Android Resources XML** from step 3's
     "Get resources". *Empty → "Invalid classname: Root element is missing"
     (that error = empty XML box, not a classname problem).*
   - **Web (client) ID:** the **Web** client ID from step 2.
   - Click **Setup** (bakes the App ID into the v2 manifest meta-data).

4. **Unity Gaming Services dashboard → Authentication → ID Providers:** add
   **Google Play Games** (not "Google"). Paste the **Web** client **ID** AND the
   **client secret** from step 2. Save.

5. **This repo**: open `Assets/_Project/ScriptableObjects/GoogleAuthConfig.asset`
   and replace the placeholder `_webClientId` with the same **Web** client ID.
   (Also the "setup not done yet" tripwire — sign-in refuses to start while it's
   the placeholder.)

6. **Minification:** keep `Assets/Plugins/Android/proguard-user.txt` and the
   "Custom Proguard File" Player Setting enabled — R8 strips
   `com.google.android.gms.games.**` otherwise (runtime `ClassNotFoundException`).

7. **Device smoke test** (Development Build, device signed with the keystore
   whose SHA-1 you registered):
   - First Google sign-in → the choose-name panel appears → enter a name → game.
   - Reinstall / clear app data → sign in again → straight into the game.
