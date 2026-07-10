# Settings Panel (Logout + Audio Volume) — Design

## Context

No settings UI exists anywhere in the project, and no audio system exists either (`AudioSource`/`AudioMixer` are unused across `Assets/_Project/Scripts`). `IAuthService.SignOutAsync()` already exists on both `AuthService` and `LocalMockAuthService`, but nothing in the game currently calls it outside `CloudCodeTestHarness` (a dev-only debug tool) — there is no in-game logout flow, and no FSM path back from gameplay to `AuthState`.

**Milestone scope note:** the architecture doc (`Social_Universe_Architecture.md` §8, M10 — Safety, Settings & Platform) formally scopes a full `SettingsService` (gyro/notifications/reduce-motion/chat-filter) to the `Safety/` folder, well beyond the current milestone (M6 — Drones & Mining Depth per project memory). This design deliberately builds only a narrow slice — logout + music/SFX volume — but places it in the architecturally-correct `Safety/` location so M10 can extend it later without a namespace migration. It does **not** implement age-gate, moderation, notifications, gyro, or chat-filter settings — those remain M10 scope.

**Existing patterns this design follows:**
- Modal show/hide via `gameObject.SetActive` — see `DisplayNameModal`.
- Optional-button wiring in `HUDController` (`_button?.onClick.AddListener(...)`).
- App-wide singletons (chat, friends, profile) registered in `RootLifetimeScope`, not `ProjectLifetimeScope` — `Core.asmdef` only references `VContainer`/`SocialUniverse.Config`, so it cannot depend on `Social`/`Net` directly.
- Sign-in completion is broadcast via `EventBus.Publish(new PlayerReadyEvent())`, consumed by `SocialServicesInitializer` to bring chat/friends online. Logout needs the symmetric teardown.
- `BootState` is the only place today that loads the Auth scene, via `LifetimeScope.EnqueueParent(_rootScope)` so `AuthSceneScope` inherits the root container.

## Goal

A gear icon in the Planet HUD opens a Settings panel with:
1. Music volume slider
2. SFX volume slider
3. Logout button (with a Yes/No confirm step) that signs the player out and returns them to the Auth scene
4. App version label
5. Close button

Volume changes apply immediately and persist across sessions. Logging out fully tears down the current gameplay scene and social connections, then lands the player back on the Auth screen exactly as if they'd just launched the app signed-out.

## Components

### 1. `Safety/IAudioSettingsService.cs` (new, `SocialUniverse.Safety`)

```csharp
public interface IAudioSettingsService
{
    float MusicVolume01 { get; }
    float SfxVolume01   { get; }

    event Action<float> OnMusicVolumeChanged;
    event Action<float> OnSfxVolumeChanged;

    void SetMusicVolume(float value01);
    void SetSfxVolume(float value01);
}
```

### 2. `Safety/AudioSettingsService.cs` (new)

Single implementation — this is local device state, not server-authoritative, so no `LocalMock*` split is needed (unlike `IEconomyService`/`IAuthService`). Constructor takes `AudioConfig`. On construction, reads persisted values from `PlayerPrefs` (default 1.0 if unset), applies them to the mixer. `SetMusicVolume`/`SetSfxVolume` clamp to [0,1], write to `PlayerPrefs`, convert linear→dB (`value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f`, the standard Unity mixer-slider conversion) and call `AudioMixer.SetFloat`, then raise the change event.

### 3. `Config/AudioConfig.cs` (new ScriptableObject, `SocialUniverse.Config`)

```csharp
[CreateAssetMenu(menuName = "SocialUniverse/Audio Config")]
public class AudioConfig : ScriptableObject
{
    public AudioMixer Mixer;
    public string MusicVolumeParam = "MusicVolume";
    public string SfxVolumeParam   = "SFXVolume";
}
```

Follows the existing `SocialConfig`/`EconomyConfig` convention (rule 3 — tunables/asset references live in inspector-editable SOs, not hardcoded).

### 4. `Core/SaveKeys.cs` (extended)

```csharp
public const string MusicVolume = "settings_music_volume";
public const string SfxVolume   = "settings_sfx_volume";
```

### 5. `Core/PlayerLoggedOutEvent.cs` (new, mirrors `PlayerReadyEvent`)

```csharp
public class PlayerLoggedOutEvent { }
```

### 6. `Core/LogoutState.cs` (new `IGameState`)

```csharp
public class LogoutState : IGameState
{
    private readonly IAuthService     _auth;
    private readonly SceneLoader      _sceneLoader;
    private readonly GameStateMachine _fsm;
    private readonly IObjectResolver  _resolver;
    private readonly LifetimeScope    _rootScope;

    public void Enter() => _ = RunAsync();
    public void Tick()  { }
    public void Exit()  { }

    private async Task RunAsync()
    {
        await _auth.SignOutAsync();
        EventBus.Publish(new PlayerLoggedOutEvent());

        using (LifetimeScope.EnqueueParent(_rootScope))
            await _sceneLoader.LoadAsync(Constants.SceneNames.Auth);

        _fsm.TransitionTo(_resolver.Resolve<AuthState>());
    }
}
```

Transitioning into `LogoutState` from `PlanetState`/`HubState` triggers the *outgoing* state's existing `Exit()` first (via `GameStateMachine.TransitionTo`) — that already unloads Planet/SolarSystem/LoadingScreen scenes, so `LogoutState` doesn't duplicate any scene-teardown logic. Registered as `Lifetime.Singleton` in `ProjectLifetimeScope`, same as the other states.

### 7. `App/SocialServicesInitializer.cs` (extended)

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

private async void OnPlayerLoggedOut(PlayerLoggedOutEvent _)
{
    try { await _chat.DisconnectAsync(); }
    catch (Exception ex) { SULog.Warn($"SocialServicesInitializer: chat disconnect failed ({ex.Message})", SULog.Channel.Social); }
}
```

Symmetric to the existing `OnPlayerReady` connect flow. Non-fatal on failure — logout must not get stuck because Vivox teardown errored.

### 8. `UI/SettingsPanel.cs` (new)

Same modal shape as `DisplayNameModal`: `Awake()` wires listeners and hides itself; `Open()`/`Close()` toggle `SetActive`.

```csharp
[SerializeField] private Slider _musicSlider;
[SerializeField] private Slider _sfxSlider;
[SerializeField] private Button _logoutButton;
[SerializeField] private GameObject _logoutConfirmPanel; // inline Yes/No sub-panel
[SerializeField] private Button _logoutConfirmYes;
[SerializeField] private Button _logoutConfirmNo;
[SerializeField] private Button _closeButton;
[SerializeField] private TMP_Text _versionText;

[Inject] private IAudioSettingsService _audio;
[Inject] private GameStateMachine      _fsm;
[Inject] private IObjectResolver       _resolver;

public void Open()
{
    _musicSlider.SetValueWithoutNotify(_audio.MusicVolume01);
    _sfxSlider.SetValueWithoutNotify(_audio.SfxVolume01);
    _logoutConfirmPanel.SetActive(false);
    _versionText.text = $"v{Application.version}";
    gameObject.SetActive(true);
}
```

- Slider `onValueChanged` → `_audio.SetMusicVolume`/`SetSfxVolume` directly (no extra debouncing — matches how `HUDController._fuelSlider` etc. are driven directly by state).
- `_logoutButton` → shows `_logoutConfirmPanel`.
- `_logoutConfirmYes` → disables all buttons (prevents double-tap) and calls `_fsm.TransitionTo(_resolver.Resolve<LogoutState>())`. The panel's own GameObject is destroyed shortly after when the Planet scene unloads, so no further UI state handling is needed post-transition.
- `_logoutConfirmNo` → hides `_logoutConfirmPanel`.

### 9. `UI/HUDController.cs` (extended)

```csharp
[SerializeField] private Button        _settingsButton;
[SerializeField] private SettingsPanel _settingsPanel;
```

```csharp
_settingsButton?.onClick.AddListener(() => _settingsPanel?.Open());
```

Same optional-reference convention already used for `_verifyEmailButton`/`_usernameButton`.

### 10. DI registration (`App/RootLifetimeScope.cs`)

```csharp
builder.RegisterInstance(_audioConfig);
builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();
```

Registered once, app-wide — audio settings must apply and persist regardless of which scene is active, same reasoning as `ProfileService`.

### 11. New assembly: `Assets/_Project/Scripts/Safety/SocialUniverse.Safety.asmdef`

References `VContainer`, `SocialUniverse.Config` (for `AudioConfig`) — mirrors `SocialUniverse.Core.asmdef`'s reference list.

### 12. Unity scene/prefab work (via Unity MCP tools, not plain C#)

- Add a gear-icon `Button` to the HUD prefab/canvas, wire to `HUDController._settingsButton`.
- Build the `SettingsPanel` GameObject hierarchy (sliders, logout button + inline confirm sub-panel, close button, version text) as a child of the HUD canvas, matching `DisplayNameModal`'s structure; wire serialized fields.
- Create the `AudioConfig` asset under `Assets/_Project/ScriptableObjects/`, create an `AudioMixer` asset with `Music`/`SFX` groups and exposed `MusicVolume`/`SFXVolume` parameters, assign both into the new asset.

## Data Flow

**Volume change:** slider drag → `SettingsPanel` calls `IAudioSettingsService.SetMusicVolume/SetSfxVolume` → clamps, persists to `PlayerPrefs`, converts to dB, applies to the injected `AudioMixer` → change event fires (available for any future UI that wants to mirror the value, e.g. a HUD mute icon — none exists yet).

**Logout:** confirm tap → `SettingsPanel` resolves and transitions to `LogoutState` → outgoing state's `Exit()` unloads its scene(s) (existing behavior, untouched) → `LogoutState.Enter()` awaits `SignOutAsync()`, publishes `PlayerLoggedOutEvent` (→ `SocialServicesInitializer` disconnects chat), loads the Auth scene under the root scope, transitions FSM to `AuthState` → `AuthState.Enter()` sees `IsSignedIn == false` and waits for `PlayerReadyEvent`, i.e. behaves exactly like a fresh cold launch that never had a cached session.

## Error Handling

- `SignOutAsync()` failure: not caught inside `LogoutState` — same fire-and-forget-with-`SULog`-on-failure convention as `BootState`/`PlanetState`'s async `Enter`/`Exit`. If this turns out to need a user-facing retry, that's an M10-scope UX concern, not this pass.
- Chat disconnect failure: caught and logged inside `SocialServicesInitializer`, non-fatal — logout must still complete even if Vivox teardown errors.
- Volume outside [0,1] (e.g. a bad persisted value from a manual PlayerPrefs edit): clamped on every read/write in `AudioSettingsService`.
- Missing `AudioConfig`/`Mixer` reference: out of scope to guard defensively — same convention as other required SO references in this codebase (e.g. `HUDController`'s injected `PlanetDefinition`), a missing inspector assignment is a setup bug caught immediately in the editor, not a runtime case to handle gracefully.

## Testing

- `Safety/AudioSettingsServiceTests.cs` (new, EditMode): set/get round-trip, clamping to [0,1], persistence across a fresh `AudioSettingsService` instance (simulating app restart), change events fire with the new value, correct dB written to a real test `AudioMixer` asset for both boundary (0, 1) and mid-range values.
- `Core/LogoutStateTests.cs` (new, EditMode): `Enter()` calls `SignOutAsync()` on a fake `IAuthService` and ends with the FSM's `Current` being the resolved `AuthState` instance. Scene-loading itself isn't asserted — same limitation `GameStateMachineTests` already accepts for other states' scene work.
- Run existing `GameStateMachineTests`/`EventBusTests` to confirm no regressions from the new state/event.
- No automated coverage for `SettingsPanel`/`HUDController` themselves — consistent with this codebase's existing convention that MonoBehaviour UI is manually verified (see prior specs' Testing sections).
- Manual Play Mode smoke test (mock backend/dev mode): open Settings from the HUD gear icon, drag both sliders and confirm audible/mixer-level change (if placeholder clips are present) or at minimum confirm `PlayerPrefs` values update, tap Logout → Yes → confirm the Planet scene unloads and the Auth screen appears, confirm a subsequent sign-in works normally (no leftover state from the previous session), confirm chat reconnects correctly on the next sign-in.
