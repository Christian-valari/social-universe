# Settings Panel (Logout + Audio Volume) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Settings panel (music/SFX volume sliders, logout with confirm, app version, close) reachable from a gear icon in the Planet HUD.

**Architecture:** New `Safety/` assembly hosts `IAudioSettingsService`/`AudioSettingsService` (local `PlayerPrefs`-backed volume state applied to a `Config/AudioConfig` SO's `AudioMixer`). A new `Core/LogoutState` reverses `BootState`: signs out via the existing `IAuthService.SignOutAsync()`, publishes `PlayerLoggedOutEvent` (consumed by `SocialServicesInitializer` to disconnect chat, symmetric to its existing `PlayerReadyEvent` handler), reloads the Auth scene, and hands off to `AuthState`. `UI/SettingsPanel` (same show/hide modal shape as `DisplayNameModal`) drives both, wired into `HUDController` via a new gear-icon button.

**Tech Stack:** Unity 6 (6000.3.12f1), URP, VContainer (DI), NUnit (EditMode tests), TextMeshPro.

## Global Constraints

- Namespace must match folder exactly (`Safety/` → `SocialUniverse.Safety`, etc.) — see CLAUDE.md Project Structure table.
- `Core.asmdef` may not reference `Social`/`Net`/`App` — cross-layer communication from Core goes through `EventBus`, never a direct type reference.
- Tunable/asset references (mixer, param names) live in a `Config/` ScriptableObject, not hardcoded — see CLAUDE.md Architecture Rule 3.
- No new backend/server calls — this feature is entirely local/client-side except for the already-existing `IAuthService.SignOutAsync()` and `IChatService.DisconnectAsync()`.
- Full M10 `SettingsService` (age-gate, moderation, notifications, chat-filter) is explicitly **out of scope** — this plan only builds the audio + logout slice. See `docs/superpowers/specs/2026-07-10-settings-panel-design.md`.
- Follow existing codebase convention: concrete `IGameState` implementations (`BootState`, `AuthState`, `PlanetState`, `HubState`, …) and MonoBehaviour UI (`DisplayNameModal`, `HUDController`, …) have **no automated test coverage** in this codebase — only `GameStateMachine` itself and pure-logic services are unit tested. This plan follows that convention rather than inventing new test infrastructure for `LogoutState`/`SettingsPanel`.

---

## Task 1: `IAudioSettingsService` + `AudioSettingsService` (Safety assembly)

**Files:**
- Create: `Assets/_Project/Scripts/Safety/SocialUniverse.Safety.asmdef`
- Create: `Assets/_Project/Scripts/Safety/IAudioSettingsService.cs`
- Create: `Assets/_Project/Scripts/Safety/AudioSettingsService.cs`
- Create: `Assets/_Project/Scripts/Config/AudioConfig.cs`
- Modify: `Assets/_Project/Scripts/Core/SaveKeys.cs`
- Modify: `Assets/_Project/Scripts/Core/SULog.cs`
- Modify: `Assets/_Project/Tests/EditMode/SocialUniverse.Tests.asmdef`
- Create: `Assets/_Project/Tests/EditMode/Safety/AudioSettingsServiceTests.cs`

**Interfaces:**
- Produces: `SocialUniverse.Safety.IAudioSettingsService` with `float MusicVolume01 { get; }`, `float SfxVolume01 { get; }`, `event Action<float> OnMusicVolumeChanged`, `event Action<float> OnSfxVolumeChanged`, `void SetMusicVolume(float value01)`, `void SetSfxVolume(float value01)`.
- Produces: `SocialUniverse.Safety.AudioSettingsService` — constructor `AudioSettingsService(AudioConfig config)`; also exposes `public static float LinearToDecibel(float value01)`.
- Produces: `SocialUniverse.Config.AudioConfig` (ScriptableObject) — `AudioMixer Mixer`, `string MusicVolumeParam`, `string SfxVolumeParam` (read-only properties backed by `[SerializeField]` fields, default param names `"MusicVolume"`/`"SFXVolume"`).
- Produces: `SocialUniverse.Core.SaveKeys.MusicVolume`, `SocialUniverse.Core.SaveKeys.SfxVolume` (both `string` consts).
- Produces: `SocialUniverse.Core.SULog.Channel.Safety` (new flag, value `1 << 8`).

- [ ] **Step 1: Create the Safety assembly definition**

Create `Assets/_Project/Scripts/Safety/SocialUniverse.Safety.asmdef`:

```json
{
    "name": "SocialUniverse.Safety",
    "rootNamespace": "SocialUniverse.Safety",
    "references": [
        "VContainer",
        "SocialUniverse.Config"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Add the `Safety` log channel**

Edit `Assets/_Project/Scripts/Core/SULog.cs` — add a new flag after `Travel`:

```csharp
            Travel     = 1 << 7,
            Safety     = 1 << 8,
            All        = ~0
```

- [ ] **Step 3: Add the volume `SaveKeys`**

Edit `Assets/_Project/Scripts/Core/SaveKeys.cs` — add after `AuthSession`:

```csharp
        public const string AuthSession    = "auth_session_player_id";
        public const string MusicVolume    = "settings_music_volume";
        public const string SfxVolume      = "settings_sfx_volume";
```

- [ ] **Step 4: Create `AudioConfig`**

Create `Assets/_Project/Scripts/Config/AudioConfig.cs`:

```csharp
using UnityEngine;
using UnityEngine.Audio;

namespace SocialUniverse.Config
{
    // Audio mixer wiring for the Settings panel's volume sliders. Kept as a
    // data asset (Architecture Rule 3) rather than hardcoded on the service
    // so the mixer/parameter names are inspector-editable and swappable
    // without touching code.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AudioConfig", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private string _musicVolumeParam = "MusicVolume";
        [SerializeField] private string _sfxVolumeParam   = "SFXVolume";

        public AudioMixer Mixer          => _mixer;
        public string MusicVolumeParam   => _musicVolumeParam;
        public string SfxVolumeParam     => _sfxVolumeParam;
    }
}
```

- [ ] **Step 5: Create `IAudioSettingsService`**

Create `Assets/_Project/Scripts/Safety/IAudioSettingsService.cs`:

```csharp
using System;

namespace SocialUniverse.Safety
{
    public interface IAudioSettingsService
    {
        float MusicVolume01 { get; }
        float SfxVolume01   { get; }

        event Action<float> OnMusicVolumeChanged;
        event Action<float> OnSfxVolumeChanged;

        void SetMusicVolume(float value01);
        void SetSfxVolume(float value01);
    }
}
```

- [ ] **Step 6: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Safety/AudioSettingsServiceTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Safety;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class AudioSettingsServiceTests
    {
        private AudioConfig _config;
        private AudioSettingsService _audio;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKeys.MusicVolume);
            PlayerPrefs.DeleteKey(SaveKeys.SfxVolume);

            _config = ScriptableObject.CreateInstance<AudioConfig>();
            _audio  = new AudioSettingsService(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            PlayerPrefs.DeleteKey(SaveKeys.MusicVolume);
            PlayerPrefs.DeleteKey(SaveKeys.SfxVolume);
        }

        [Test]
        public void Defaults_to_full_volume_when_nothing_persisted()
        {
            Assert.AreEqual(1f, _audio.MusicVolume01);
            Assert.AreEqual(1f, _audio.SfxVolume01);
        }

        [Test]
        public void SetMusicVolume_updates_property()
        {
            _audio.SetMusicVolume(0.4f);
            Assert.AreEqual(0.4f, _audio.MusicVolume01, 0.0001f);
        }

        [Test]
        public void SetSfxVolume_clamps_above_one()
        {
            _audio.SetSfxVolume(3f);
            Assert.AreEqual(1f, _audio.SfxVolume01);
        }

        [Test]
        public void SetMusicVolume_clamps_below_zero()
        {
            _audio.SetMusicVolume(-2f);
            Assert.AreEqual(0f, _audio.MusicVolume01);
        }

        [Test]
        public void SetMusicVolume_persists_across_fresh_instance()
        {
            _audio.SetMusicVolume(0.25f);

            var reloaded = new AudioSettingsService(_config);

            Assert.AreEqual(0.25f, reloaded.MusicVolume01, 0.0001f);
        }

        [Test]
        public void SetMusicVolume_fires_change_event_with_new_value()
        {
            float? raised = null;
            _audio.OnMusicVolumeChanged += v => raised = v;

            _audio.SetMusicVolume(0.6f);

            Assert.AreEqual(0.6f, raised.Value, 0.0001f);
        }

        [Test]
        public void SetSfxVolume_fires_change_event_with_new_value()
        {
            float? raised = null;
            _audio.OnSfxVolumeChanged += v => raised = v;

            _audio.SetSfxVolume(0.2f);

            Assert.AreEqual(0.2f, raised.Value, 0.0001f);
        }

        [TestCase(0f, -80f)]
        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.0206f)]
        public void LinearToDecibel_matches_standard_mixer_curve(float linear, float expectedDb)
        {
            Assert.AreEqual(expectedDb, AudioSettingsService.LinearToDecibel(linear), 0.01f);
        }
    }
}
```

- [ ] **Step 7: Add the `Safety` reference to the EditMode test assembly**

Edit `Assets/_Project/Tests/EditMode/SocialUniverse.Tests.asmdef` — add `"SocialUniverse.Safety"` to `references`:

```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "SocialUniverse.Core",
        "SocialUniverse.Config",
        "SocialUniverse.Economy",
        "SocialUniverse.Mining",
        "SocialUniverse.Progression",
        "SocialUniverse.Net",
        "SocialUniverse.Social",
        "SocialUniverse.World",
        "SocialUniverse.Travel",
        "SocialUniverse.Safety"
    ],
```

- [ ] **Step 8: Confirm the tests fail to compile / fail to run (no implementation yet)**

Use `mcp__UnityMCP__refresh_unity` to force a domain reload, then `mcp__UnityMCP__read_console` filtered to errors. Expected: compiler error, `AudioSettingsService` does not exist (or `run_tests` reports the suite can't be found). This confirms the test file is wired up before the implementation exists.

- [ ] **Step 9: Implement `AudioSettingsService`**

Create `Assets/_Project/Scripts/Safety/AudioSettingsService.cs`:

```csharp
using System;
using SocialUniverse.Config;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Safety
{
    // Local device audio preferences (music/SFX volume) — not server-authoritative,
    // so unlike IEconomyService/IAuthService there is no LocalMock/real split, just
    // this one implementation. Persists to PlayerPrefs and applies to AudioConfig's
    // mixer using the standard Unity linear-to-decibel slider conversion.
    public class AudioSettingsService : IAudioSettingsService
    {
        private readonly AudioConfig _config;
        private float _musicVolume01;
        private float _sfxVolume01;

        public float MusicVolume01 => _musicVolume01;
        public float SfxVolume01   => _sfxVolume01;

        public event Action<float> OnMusicVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;

        public AudioSettingsService(AudioConfig config)
        {
            _config = config;
            _musicVolume01 = PlayerPrefs.GetFloat(SaveKeys.MusicVolume, 1f);
            _sfxVolume01   = PlayerPrefs.GetFloat(SaveKeys.SfxVolume, 1f);
            ApplyToMixer(_config.MusicVolumeParam, _musicVolume01);
            ApplyToMixer(_config.SfxVolumeParam, _sfxVolume01);
        }

        public void SetMusicVolume(float value01)
        {
            _musicVolume01 = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SaveKeys.MusicVolume, _musicVolume01);
            PlayerPrefs.Save();
            ApplyToMixer(_config.MusicVolumeParam, _musicVolume01);
            OnMusicVolumeChanged?.Invoke(_musicVolume01);
        }

        public void SetSfxVolume(float value01)
        {
            _sfxVolume01 = Mathf.Clamp01(value01);
            PlayerPrefs.SetFloat(SaveKeys.SfxVolume, _sfxVolume01);
            PlayerPrefs.Save();
            ApplyToMixer(_config.SfxVolumeParam, _sfxVolume01);
            OnSfxVolumeChanged?.Invoke(_sfxVolume01);
        }

        private void ApplyToMixer(string param, float value01)
        {
            // Mixer is optional at the service level (kept unassigned in unit
            // tests, guaranteed assigned in the real AudioConfig asset — see
            // Task 4). A missing mixer here just means volume still persists
            // as a preference without any audible effect yet.
            if (_config.Mixer == null) return;
            _config.Mixer.SetFloat(param, LinearToDecibel(value01));
        }

        public static float LinearToDecibel(float value01) =>
            value01 <= 0.0001f ? -80f : Mathf.Log10(value01) * 20f;
    }
}
```

- [ ] **Step 10: Run the tests and confirm they pass**

Use `mcp__UnityMCP__run_tests` with `testPlatform: "EditMode"` and a filter/assembly of `SocialUniverse.Tests` (or the full EditMode suite). Expected: all `AudioSettingsServiceTests` cases pass, and no existing EditMode tests regress. If the MCP tool is unavailable, run:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode
```

and check `results.xml` for `AudioSettingsServiceTests` all passing.

- [ ] **Step 11: Commit**

```bash
git add Assets/_Project/Scripts/Safety Assets/_Project/Scripts/Config/AudioConfig.cs Assets/_Project/Scripts/Config/AudioConfig.cs.meta Assets/_Project/Scripts/Core/SaveKeys.cs Assets/_Project/Scripts/Core/SULog.cs Assets/_Project/Tests/EditMode/SocialUniverse.Tests.asmdef Assets/_Project/Tests/EditMode/Safety
git commit -m "$(cat <<'EOF'
Add AudioSettingsService (music/SFX volume) in new Safety assembly

EOF
)"
```

(Include the `.meta` files Unity generates for every new `.cs`/`.asmdef`/folder — `git status` first and add whatever `.meta` files appeared alongside the files above.)

---

## Task 2: Logout flow (`LogoutState` + `PlayerLoggedOutEvent`)

**Files:**
- Create: `Assets/_Project/Scripts/Core/PlayerLoggedOutEvent.cs`
- Create: `Assets/_Project/Scripts/Core/LogoutState.cs`
- Modify: `Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs`
- Modify: `Assets/_Project/Scripts/App/SocialServicesInitializer.cs`

**Interfaces:**
- Consumes: `SocialUniverse.Core.IAuthService.SignOutAsync()` (existing), `SocialUniverse.Core.SceneLoader.LoadAsync(string, LoadSceneMode)` (existing), `SocialUniverse.Core.Constants.SceneNames.Auth` (existing), `SocialUniverse.Core.AuthState` (existing, resolved via `IObjectResolver`), `SocialUniverse.Social.IChatService.DisconnectAsync()` (existing).
- Produces: `SocialUniverse.Core.PlayerLoggedOutEvent` (readonly struct) — published once logout's sign-out completes, before the Auth scene loads.
- Produces: `SocialUniverse.Core.LogoutState : IGameState` — transition target for "log out from anywhere in gameplay."

No automated tests for this task — see Global Constraints: no concrete `IGameState` in this codebase has unit coverage (`BootState`/`AuthState`/`PlanetState`/`HubState`/`TravelState` are all untested, verified manually only), because they depend on real Unity scene loading (`SceneManager.LoadSceneAsync`) and a real `LifetimeScope`, neither of which this codebase mocks anywhere. `LogoutState` follows the same convention. It's verified manually in Task 4's smoke test instead.

- [ ] **Step 1: Create `PlayerLoggedOutEvent`**

Create `Assets/_Project/Scripts/Core/PlayerLoggedOutEvent.cs`:

```csharp
namespace SocialUniverse.Core
{
    // Published by LogoutState once sign-out completes, before the Auth scene
    // loads. SocialServicesInitializer subscribes to disconnect chat — the
    // symmetric teardown to what it does on PlayerReadyEvent.
    public readonly struct PlayerLoggedOutEvent { }
}
```

- [ ] **Step 2: Create `LogoutState`**

Create `Assets/_Project/Scripts/Core/LogoutState.cs`:

```csharp
using System.Threading.Tasks;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.Core
{
    // Reverses BootState/AuthState: signs the player out, publishes
    // PlayerLoggedOutEvent, reloads the Auth scene under the root container
    // (same LifetimeScope.EnqueueParent trick BootState uses so AuthSceneScope
    // can resolve IAuthService from root), and hands off to AuthState — which
    // then behaves exactly like a fresh cold launch with no cached session.
    //
    // Transitioning into this state from PlanetState/HubState already triggers
    // the outgoing state's Exit() via GameStateMachine.TransitionTo, which
    // unloads its scene(s) — no scene-teardown logic is duplicated here.
    public class LogoutState : IGameState
    {
        private readonly IAuthService     _auth;
        private readonly SceneLoader      _sceneLoader;
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;
        private readonly LifetimeScope    _rootScope;

        public LogoutState(IAuthService auth, SceneLoader sceneLoader, GameStateMachine fsm,
            IObjectResolver resolver, LifetimeScope rootScope)
        {
            _auth        = auth;
            _sceneLoader = sceneLoader;
            _fsm         = fsm;
            _resolver    = resolver;
            _rootScope   = rootScope;
        }

        public void Enter() => _ = RunAsync();
        public void Tick()  { }
        public void Exit()  { }

        private async Task RunAsync()
        {
            SULog.Info("Logout: signing out");
            await _auth.SignOutAsync();
            EventBus.Publish(new PlayerLoggedOutEvent());

            using (LifetimeScope.EnqueueParent(_rootScope))
            {
                await _sceneLoader.LoadAsync(Constants.SceneNames.Auth);
            }
            _fsm.TransitionTo(_resolver.Resolve<AuthState>());
        }
    }
}
```

- [ ] **Step 3: Register `LogoutState` in the DI container**

Edit `Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs` — add alongside the other states:

```csharp
            builder.Register<BootState>(Lifetime.Singleton);
            builder.Register<AuthState>(Lifetime.Singleton);
            builder.Register<HubState>(Lifetime.Singleton);
            builder.Register<TravelState>(Lifetime.Singleton);
            builder.Register<TravelLoadingState>(Lifetime.Singleton);
            builder.Register<PlanetState>(Lifetime.Singleton);
            builder.Register<ActiveMiningState>(Lifetime.Singleton);
            builder.Register<LogoutState>(Lifetime.Singleton);
```

- [ ] **Step 4: Wire chat teardown on logout**

Edit `Assets/_Project/Scripts/App/SocialServicesInitializer.cs` — update `Start`/`Dispose` and add the handler:

```csharp
        public void Start()
        {
            EventBus.Subscribe<PlayerReadyEvent>(OnPlayerReady);
            EventBus.Subscribe<PlayerLoggedOutEvent>(OnPlayerLoggedOut);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);
            EventBus.Unsubscribe<PlayerLoggedOutEvent>(OnPlayerLoggedOut);
        }
```

Add the handler method (near `OnPlayerReady`):

```csharp
        private async void OnPlayerLoggedOut(PlayerLoggedOutEvent _)
        {
            try
            {
                await _chat.DisconnectAsync();
                SULog.Info("SocialServicesInitializer: chat disconnected on logout", SULog.Channel.Social);
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: chat disconnect failed ({ex.Message})", SULog.Channel.Social);
            }
        }
```

- [ ] **Step 5: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console` filtered to errors/warnings. Expected: no compile errors. `Core.asmdef` still doesn't reference `App`/`Social` — `LogoutState` only touches types already in `Core`.

- [ ] **Step 6: Run the full EditMode suite to confirm no regressions**

Use `mcp__UnityMCP__run_tests` with `testPlatform: "EditMode"` (no filter — full suite). Expected: same pass count as before this task (this task added no new automated tests, so the count should be unchanged from Task 1's end state).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Core/PlayerLoggedOutEvent.cs Assets/_Project/Scripts/Core/PlayerLoggedOutEvent.cs.meta Assets/_Project/Scripts/Core/LogoutState.cs Assets/_Project/Scripts/Core/LogoutState.cs.meta Assets/_Project/Scripts/Core/ProjectLifetimeScope.cs Assets/_Project/Scripts/App/SocialServicesInitializer.cs
git commit -m "$(cat <<'EOF'
Add LogoutState and chat-disconnect-on-logout wiring

EOF
)"
```

---

## Task 3: `SettingsPanel` + `HUDController` wiring + DI registration

**Files:**
- Create: `Assets/_Project/Scripts/UI/SettingsPanel.cs`
- Modify: `Assets/_Project/Scripts/UI/HUDController.cs`
- Modify: `Assets/_Project/Scripts/App/RootLifetimeScope.cs`
- Modify: `Assets/_Project/Scripts/App/SocialUniverse.App.asmdef`
- Modify: `Assets/_Project/Scripts/UI/SocialUniverse.UI.asmdef`

**Interfaces:**
- Consumes: `SocialUniverse.Safety.IAudioSettingsService` (Task 1), `SocialUniverse.Core.LogoutState` (Task 2), `SocialUniverse.Core.GameStateMachine.TransitionTo(IGameState)` (existing), `VContainer.IObjectResolver.Resolve<T>()` (existing).
- Produces: `SocialUniverse.UI.SettingsPanel` — `public void Open()`, `public void Close()` (mirrors `DisplayNameModal`'s public surface).

No automated tests — MonoBehaviour UI is manually verified in this codebase (see `DisplayNameModal`, `ChatMessageItemView`, etc.). Verified via compile check here and the full manual smoke test in Task 4.

- [ ] **Step 1: Add the `Safety` reference to `App` and `UI` assemblies**

Edit `Assets/_Project/Scripts/App/SocialUniverse.App.asmdef` — add `"SocialUniverse.Safety"` to `references`:

```json
    "references": [
        "VContainer",
        "SocialUniverse.Core",
        "SocialUniverse.Config",
        "SocialUniverse.Net",
        "SocialUniverse.World",
        "SocialUniverse.Mining",
        "SocialUniverse.Economy",
        "SocialUniverse.Social",
        "SocialUniverse.Progression",
        "SocialUniverse.UI",
        "SocialUniverse.Travel",
        "SocialUniverse.Safety"
    ],
```

Edit `Assets/_Project/Scripts/UI/SocialUniverse.UI.asmdef` — add `"SocialUniverse.Safety"` to `references`:

```json
    "references": [
        "VContainer",
        "SocialUniverse.Core",
        "SocialUniverse.Config",
        "SocialUniverse.World",
        "SocialUniverse.Mining",
        "SocialUniverse.Economy",
        "SocialUniverse.Progression",
        "SocialUniverse.Travel",
        "SocialUniverse.Net",
        "SocialUniverse.Social",
        "SocialUniverse.Safety",
        "Unity.TextMeshPro"
    ],
```

- [ ] **Step 2: Register `AudioConfig`/`AudioSettingsService` in `RootLifetimeScope`**

Edit `Assets/_Project/Scripts/App/RootLifetimeScope.cs` — add the `using`, a serialized field, and registration:

```csharp
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Safety;
using SocialUniverse.Social;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace SocialUniverse.App
{
    public class RootLifetimeScope : ProjectLifetimeScope
    {
        [SerializeField] private bool _devMode = false;
        [SerializeField] private SocialConfig _socialConfig;
        [SerializeField] private AudioConfig  _audioConfig;
```

Insert right after the existing `builder.Register<ProfileService>(Lifetime.Singleton);` line (the last line of the "M4 social layer" block) and before the `if (_devMode)` block that follows it:

```csharp
            builder.Register<ProfileService>(Lifetime.Singleton);

            // Audio settings: local device preference, spans scenes like the
            // other app-wide singletons above.
            builder.RegisterInstance(_audioConfig);
            builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();

            if (_devMode)
                builder.Register<LocalMockPresenceService>(Lifetime.Singleton).As<IPresenceService>();
```

- [ ] **Step 3: Create `SettingsPanel`**

Create `Assets/_Project/Scripts/UI/SettingsPanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Safety;

namespace SocialUniverse.UI
{
    // Settings modal: music/SFX volume, logout (with inline Yes/No confirm),
    // app version, close. Same show/hide modal shape as DisplayNameModal.
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Button _logoutButton;
        [SerializeField] private GameObject _logoutConfirmPanel;
        [SerializeField] private Button _logoutConfirmYes;
        [SerializeField] private Button _logoutConfirmNo;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _versionText;

        [Inject] private IAudioSettingsService _audio;
        [Inject] private GameStateMachine      _fsm;
        [Inject] private IObjectResolver       _resolver;

        private void Awake()
        {
            _musicSlider.onValueChanged.AddListener(_audio.SetMusicVolume);
            _sfxSlider.onValueChanged.AddListener(_audio.SetSfxVolume);
            _logoutButton.onClick.AddListener(() => _logoutConfirmPanel.SetActive(true));
            _logoutConfirmYes.onClick.AddListener(OnLogoutConfirmed);
            _logoutConfirmNo.onClick.AddListener(() => _logoutConfirmPanel.SetActive(false));
            _closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            _musicSlider.SetValueWithoutNotify(_audio.MusicVolume01);
            _sfxSlider.SetValueWithoutNotify(_audio.SfxVolume01);
            _logoutConfirmPanel.SetActive(false);
            _versionText.text = $"v{Application.version}";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private void OnLogoutConfirmed()
        {
            SetInteractable(false);
            _fsm.TransitionTo(_resolver.Resolve<LogoutState>());
        }

        private void SetInteractable(bool interactable)
        {
            _logoutButton.interactable       = interactable;
            _logoutConfirmYes.interactable   = interactable;
            _logoutConfirmNo.interactable    = interactable;
            _closeButton.interactable        = interactable;
        }
    }
}
```

- [ ] **Step 4: Wire the gear icon into `HUDController`**

Edit `Assets/_Project/Scripts/UI/HUDController.cs` — add the serialized fields near the other modal references:

```csharp
        [SerializeField] private LandPurchaseModal _landPurchaseModal;
        [SerializeField] private TileInfoModal     _tileInfoModal;
        [SerializeField] private Button             _settingsButton;
        [SerializeField] private SettingsPanel      _settingsPanel;
```

Add the listener wiring in `Start()`, alongside the other optional-button wiring:

```csharp
            _launchButton?.onClick.AddListener(() => EventBus.Publish(new LaunchRequestedEvent()));
            _settingsButton?.onClick.AddListener(() => _settingsPanel?.Open());
```

- [ ] **Step 5: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console` filtered to errors/warnings. Expected: no compile errors. Note `_settingsButton`/`_settingsPanel` are unassigned in the Inspector until Task 4 wires the prefab/scene — that's fine, `HUDController` already tolerates unassigned optional buttons via `?.`.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/UI/SettingsPanel.cs Assets/_Project/Scripts/UI/SettingsPanel.cs.meta Assets/_Project/Scripts/UI/HUDController.cs Assets/_Project/Scripts/App/RootLifetimeScope.cs Assets/_Project/Scripts/App/SocialUniverse.App.asmdef Assets/_Project/Scripts/UI/SocialUniverse.UI.asmdef
git commit -m "$(cat <<'EOF'
Add SettingsPanel and wire it into the HUD gear icon

EOF
)"
```

---

## Task 4: Unity Editor wiring (AudioMixer, AudioConfig asset, HUD prefab/scene) + manual verification

This task has no C# — it builds the Unity assets and scene hierarchy the previous tasks' scripts reference, using Unity MCP tools, then runs the manual smoke test from the design spec.

**Files:**
- Create: `Assets/_Project/Audio/MasterMixer.mixer` (new folder)
- Create: `Assets/_Project/ScriptableObjects/AudioConfig.asset`
- Modify: Planet scene's HUD GameObject hierarchy (add gear icon button + `SettingsPanel` hierarchy)
- Modify: Bootstrap scene's `RootLifetimeScope` component (assign `_audioConfig`)

- [ ] **Step 1: Create the AudioMixer asset with exposed parameters**

Use `mcp__UnityMCP__manage_asset` (or `execute_menu_item` for `Assets/Create/Audio Mixer` if `manage_asset` doesn't support mixer creation directly) to create `Assets/_Project/Audio/MasterMixer.mixer` with two child groups, `Music` and `SFX`, under the Master group. Select the mixer, right-click each group's Volume slider in the Audio Mixer window and choose "Expose parameter", naming them exactly `MusicVolume` and `SFXVolume` (must match `AudioConfig`'s default `_musicVolumeParam`/`_sfxVolumeParam` from Task 1 Step 4).

- [ ] **Step 2: Create the `AudioConfig` asset**

Use `mcp__UnityMCP__manage_asset` to create an instance of `SocialUniverse.Config.AudioConfig` at `Assets/_Project/ScriptableObjects/AudioConfig.asset` (menu path `SocialUniverse/Config/AudioConfig`, per the `CreateAssetMenu` attribute from Task 1 Step 4). Assign its `_mixer` field to `MasterMixer.mixer` from Step 1. Leave `_musicVolumeParam`/`_sfxVolumeParam` at their defaults (`MusicVolume`/`SFXVolume`) since those match the exposed parameter names from Step 1.

- [ ] **Step 3: Assign `AudioConfig` on `RootLifetimeScope`**

Open the Bootstrap scene. Use `mcp__UnityMCP__find_gameobjects` to locate the GameObject holding the `RootLifetimeScope` component, then `mcp__UnityMCP__manage_components` (or `manage_gameobject`) to assign its `_audioConfig` field to the `AudioConfig.asset` from Step 2.

- [ ] **Step 4: Locate the HUD in the Planet scene**

Open the Planet scene. Use `mcp__UnityMCP__find_gameobjects` to locate the GameObject holding `HUDController` and inspect its existing children (e.g. the chat button, avatar button) to match their button/icon styling for the new gear icon.

- [ ] **Step 5: Add the gear icon button**

Use `mcp__UnityMCP__manage_ui` (or `manage_gameobject` + `manage_components`) to add a new `Button` (with an `Image` icon child, reuse an existing gear/settings sprite from the project's UI art if one exists, otherwise a plain placeholder sprite consistent with the other HUD icon buttons) as a sibling of the existing HUD buttons (chat/avatar/launch). Position it in an unused corner of the HUD canvas (e.g. top-left, opposite the avatar/chat cluster).

- [ ] **Step 6: Build the `SettingsPanel` hierarchy**

Use `mcp__UnityMCP__manage_ui`/`manage_gameobject` to create a `SettingsPanel`-rooted GameObject as a child of the HUD canvas (sibling of `DisplayNameModal`'s panel), containing:
- A `Slider` for music volume
- A `Slider` for SFX volume
- A `Button` for Logout
- A child `GameObject` (`LogoutConfirmPanel`) containing two `Button`s (Yes/No) and a confirmation label ("Log out?")
- A `Button` for Close
- A `TMP_Text` for the version label

Add the `SettingsPanel` component (from Task 3) to the root GameObject and wire its serialized fields (`_musicSlider`, `_sfxSlider`, `_logoutButton`, `_logoutConfirmPanel`, `_logoutConfirmYes`, `_logoutConfirmNo`, `_closeButton`, `_versionText`) to the GameObjects created above, via `manage_components`.

- [ ] **Step 7: Wire `HUDController`'s new fields**

Use `manage_components` to assign `HUDController._settingsButton` to the gear icon `Button` from Step 5, and `HUDController._settingsPanel` to the `SettingsPanel` root from Step 6.

- [ ] **Step 8: Verify compilation and scene save**

Use `mcp__UnityMCP__read_console` filtered to errors. Use `mcp__UnityMCP__manage_scene` to save both the Bootstrap and Planet scenes.

- [ ] **Step 9: Manual Play Mode smoke test**

Use `mcp__UnityMCP__manage_editor` to enter Play Mode with `RootLifetimeScope._devMode` enabled (mock backend). Then:
1. Confirm the app boots to the Planet scene (mock auto-sign-in).
2. Tap the gear icon — confirm `SettingsPanel` opens.
3. Drag the music slider — confirm `PlayerPrefs` key `settings_music_volume` updates (inspect via `mcp__UnityMCP__execute_code` reading `PlayerPrefs.GetFloat("settings_music_volume")`, or listen for the mixer's group volume changing in the Audio Mixer window if clips are later added).
4. Drag the SFX slider — same check for `settings_sfx_volume`.
5. Tap Logout — confirm the Yes/No confirm panel appears; tap No — confirm it dismisses without signing out.
6. Tap Logout again, then Yes — confirm the Planet scene unloads and the Auth screen appears (same as a fresh cold launch with no cached session).
7. Sign in again (mock) — confirm the app lands back on the Planet scene and chat reconnects (check `read_console` for the `SocialServicesInitializer: chat connected` log line from `OnPlayerReady`, confirming no leftover state from the previous session blocked reconnection).

Exit Play Mode via `manage_editor` once all checks pass.

- [ ] **Step 10: Run the full EditMode suite one more time**

Use `mcp__UnityMCP__run_tests` with `testPlatform: "EditMode"`, no filter. Expected: same pass count as Task 1 established (no regressions from the scene/asset changes, since none of this task touched test-covered code).

- [ ] **Step 11: Commit**

```bash
git add Assets/_Project/Audio Assets/_Project/ScriptableObjects/AudioConfig.asset Assets/_Project/ScriptableObjects/AudioConfig.asset.meta Assets/Scenes/Bootstrap.unity Assets/Scenes/Planet.unity
git commit -m "$(cat <<'EOF'
Wire Settings panel and audio mixer into HUD and Bootstrap scenes

EOF
)"
```

(Run `git status` first and include any other `.meta`/scene-dependency files Unity touched as part of this task — e.g. the mixer asset's own `.meta`, any new folder `.meta` files.)
