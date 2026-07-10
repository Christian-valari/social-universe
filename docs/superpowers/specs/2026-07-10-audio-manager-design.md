# Audio Manager (BGM + SFX) — Design

## Context

No audio ever plays in this game today — Task 1 of the settings-panel work (`docs/superpowers/specs/2026-07-10-settings-panel-design.md`) built the *preference* layer (`Safety/IAudioSettingsService`, `Config/AudioConfig` holding an `AudioMixer` with `Music`/`SFX` groups, volume sliders in the Settings panel) but nothing actually plays a clip through it. That work is still on the unmerged `feature/settings-panel` branch, blocked on a Unity Editor MCP-bridge outage for final verification. This design continues on that same branch — it's the natural second half of the same audio system, and depends directly on `AudioConfig`'s mixer wiring.

The project already has first-party, purpose-named audio assets checked in:
- `Assets/Audio/BGM/`: one track each for Earth, Moon, Jupiter, Mercury, Mars, Venus, Neptune, Uranus, plus two generic tracks ("Social Universe Mvp 1" and "Mvp 2.2"). **No Saturn or Pluto track exists.**
- `Assets/Audio/SFX/`: confirm/alert sounds, a "return UI" sound, chat pings, mining/asteroid sounds, coins, rocket travel/arrival sounds.
- `Assets/Plugins/UltimateCleanGUIPack/Common/Sounds/`: third-party generic UI sounds (already imported), including `Open (Button).wav`.

Nothing in the codebase references `AudioSource`, `PlayOneShot`, or any `AudioClip` today — this design is the first to wire actual playback.

**Confirmed decisions from brainstorming:**
- Mvp 2.2 → SolarSystem scene BGM **and** the Saturn/Pluto fallback. Mvp 1 → Travel scene BGM.
- Generic Open-panel SFX → `UltimateCleanGUIPack`'s `Open (Button).wav`.
- Generic Confirm SFX → `403009__inspectorj__ui-confirmation-alert-b3.wav` (one fixed choice, not randomized across the three near-identical InspectorJ variants).
- Generic Cancel/Close SFX → `Social Universe UI - return UI.wav`.

## Goal

A persistent `AudioManager` plays the correct background music for the Planet (per-planet), SolarSystem, and Travel scenes with crossfade between tracks, and plays one-shot SFX for: generic Confirm, generic Cancel/Close, panel-open, mining-complete (claim), active-mining tap-hit, new chat message, plus the project's other clearly-named cues (travel confirm, planet-observe confirm, coins/reward, asteroid-destroyed, rocket depart/arrive). Volume for both BGM and SFX is already controlled by the existing Settings panel sliders (Task 1) — this design adds no new volume UI, it only adds playback that routes through the mixer groups Task 1 already built.

## Components

### 1. `Safety/SfxId.cs` (new)

```csharp
namespace SocialUniverse.Safety
{
    public enum SfxId
    {
        Confirm,
        Cancel,
        OpenPanel,
        MiningComplete,
        ActiveMiningTap,
        NewMessage,
        TravelConfirm,
        PlanetObserveConfirm,
        CoinsReward,
        AsteroidDestroyed,
        RocketDepart,
        RocketArrive
    }
}
```

### 2. `Config/AudioCatalog.cs` (new ScriptableObject)

```csharp
using UnityEngine;
using SocialUniverse.Safety;

namespace SocialUniverse.Config
{
    [System.Serializable]
    public struct SfxEntry
    {
        public SfxId     Id;
        public AudioClip Clip;
    }

    // Data-driven catalog of every clip AudioManager can play, aside from
    // per-planet BGM (which lives on PlanetDefinition itself — see below).
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AudioCatalog", fileName = "AudioCatalog")]
    public class AudioCatalog : ScriptableObject
    {
        [Header("BGM — non-planet scenes")]
        [SerializeField] private AudioClip _solarSystemBgm;
        [SerializeField] private AudioClip _travelBgm;
        [SerializeField] private AudioClip _fallbackPlanetBgm; // planets with no BgmClip of their own (Saturn, Pluto)

        [Header("SFX")]
        [SerializeField] private SfxEntry[] _sfxEntries;

        public AudioClip SolarSystemBgm     => _solarSystemBgm;
        public AudioClip TravelBgm          => _travelBgm;
        public AudioClip FallbackPlanetBgm  => _fallbackPlanetBgm;

        public AudioClip GetSfxClip(SfxId id)
        {
            foreach (var entry in _sfxEntries)
                if (entry.Id == id) return entry.Clip;
            return null;
        }
    }
}
```

`Config.asmdef` currently has no references (checked — `"references": []`); it needs `SocialUniverse.Safety` added so it can use `SfxId`. `Safety.asmdef` already references `SocialUniverse.Config` (for `AudioConfig`, from Task 1) — adding `Config → Safety` would create a cycle. To avoid that, `SfxId` is deliberately placed in `Safety` and `AudioCatalog` (in `Config`) references it — meaning `Config.asmdef` must add a reference to `Safety.asmdef`, and `Safety.asmdef`'s existing reference to `Config.asmdef` must be checked for a cycle. **This is a real circular-reference risk** (`Safety → Config` for `AudioConfig`, `Config → Safety` for `SfxId` on `AudioCatalog`) — resolved by moving `SfxId` out of `Safety` into `Config` instead (it's a data-catalog key, not manager logic), so the dependency only flows one way: `Safety → Config` (unchanged from Task 1), never the reverse. Revised:

```csharp
// Config/SfxId.cs (moved from Safety/SfxId.cs)
namespace SocialUniverse.Config
{
    public enum SfxId { Confirm, Cancel, OpenPanel, MiningComplete, ActiveMiningTap, NewMessage,
                         TravelConfirm, PlanetObserveConfirm, CoinsReward, AsteroidDestroyed,
                         RocketDepart, RocketArrive }
}
```

`AudioCatalog.cs` then just uses `SfxId` in the same namespace, no cross-reference needed. `Safety/IAudioManager.cs`/`AudioManager.cs` (below) reference `SocialUniverse.Config.SfxId` the same way they already reference `SocialUniverse.Config.AudioConfig`/`AudioCatalog` — no new assembly edges beyond what Task 1 already established.

### 3. `Config/PlanetDefinition.cs` (extended)

One new field, following the existing private-field-plus-property pattern:

```csharp
        [SerializeField] private AudioClip _bgmClip;
        ...
        public AudioClip           BgmClip               => _bgmClip;
```

### 4. `Safety/IAudioManager.cs` (new)

```csharp
using SocialUniverse.Config;

namespace SocialUniverse.Safety
{
    public interface IAudioManager
    {
        void PlaySfx(SfxId id);
        void PlayBgmForPlanet(PlanetDefinition planet);
        void PlaySolarSystemBgm();
        void PlayTravelBgm();
    }
}
```

### 5. `Safety/AudioManager.cs` (new)

Plain C# singleton (not scene-registered — see Architecture below). Constructor takes `AudioConfig` (Task 1's mixer wiring) and `AudioCatalog` (this design's clip catalog).

```csharp
using UnityEngine;
using UnityEngine.Audio;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Safety
{
    // Persistent audio playback: BGM with crossfade (two ping-ponged AudioSources),
    // SFX as fire-and-forget one-shots. Volume is entirely the Settings panel's
    // job (Task 1's AudioSettingsService drives AudioConfig.Mixer's Music/SFX
    // groups) — this class only decides *what* plays *when*, routed through
    // those same groups so slider changes apply immediately to whatever is
    // already playing.
    public class AudioManager : IAudioManager
    {
        private const float CrossfadeSeconds = 1.5f;

        private readonly AudioCatalog _catalog;
        private readonly AudioSource  _bgmA;
        private readonly AudioSource  _bgmB;
        private readonly AudioSource  _sfx;
        private          AudioSource  _activeBgm;

        private System.Threading.Tasks.Task _crossfadeTask = System.Threading.Tasks.Task.CompletedTask;

        public AudioManager(AudioConfig config, AudioCatalog catalog)
        {
            _catalog = catalog;

            var root = new GameObject("AudioManager (runtime)");
            Object.DontDestroyOnLoad(root);

            AudioMixerGroup musicGroup = config.Mixer != null ? FindGroup(config.Mixer, "Music") : null;
            AudioMixerGroup sfxGroup   = config.Mixer != null ? FindGroup(config.Mixer, "SFX")   : null;

            _bgmA = CreateSource(root, "BgmA", musicGroup, loop: true);
            _bgmB = CreateSource(root, "BgmB", musicGroup, loop: true);
            _sfx  = CreateSource(root, "Sfx",  sfxGroup,   loop: false);
            _activeBgm = _bgmA;
        }

        public void PlaySfx(SfxId id)
        {
            var clip = _catalog.GetSfxClip(id);
            if (clip == null)
            {
                SULog.Warn($"AudioManager: no clip mapped for {id}", SULog.Channel.Core);
                return;
            }
            _sfx.PlayOneShot(clip);
        }

        public void PlayBgmForPlanet(PlanetDefinition planet)
        {
            var clip = planet != null && planet.BgmClip != null ? planet.BgmClip : _catalog.FallbackPlanetBgm;
            CrossfadeTo(clip);
        }

        public void PlaySolarSystemBgm() => CrossfadeTo(_catalog.SolarSystemBgm);
        public void PlayTravelBgm()      => CrossfadeTo(_catalog.TravelBgm);

        private void CrossfadeTo(AudioClip clip)
        {
            if (clip == null || _activeBgm.clip == clip) return;

            var incoming = _activeBgm == _bgmA ? _bgmB : _bgmA;
            var outgoing = _activeBgm;

            incoming.clip   = clip;
            incoming.volume = 0f;
            incoming.Play();
            _activeBgm = incoming;

            _crossfadeTask = FadeAsync(outgoing, incoming);
        }

        private static async System.Threading.Tasks.Task FadeAsync(AudioSource outgoing, AudioSource incoming)
        {
            float t = 0f;
            while (t < CrossfadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / CrossfadeSeconds);
                outgoing.volume = 1f - p;
                incoming.volume = p;
                await System.Threading.Tasks.Task.Yield();
            }
            outgoing.Stop();
            outgoing.volume = 1f;
            incoming.volume = 1f;
        }

        private static AudioSource CreateSource(GameObject root, string name, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            var source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = group;
            source.loop = loop;
            source.playOnAwake = false;
            return source;
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            return groups.Length > 0 ? groups[0] : null;
        }
    }
}
```

### 6. DI registration (`App/RootLifetimeScope.cs`)

```csharp
[SerializeField] private AudioCatalog _audioCatalog;
...
builder.RegisterInstance(_audioCatalog);
builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
```

Placed right after Task 1's `AudioSettingsService` registration. Mirrors the same pattern; no scene/prefab wiring needed since `AudioManager` builds its own GameObject in its constructor (see Component 5) — unlike `SettingsPanel`, there's no `RegisterComponentInHierarchy` step, so this doesn't carry the DI-registration risk that bit `SettingsPanel` in the settings-panel branch's final review.

Also needs registering identically inside `PlanetSceneScope`'s/`SolarSystemScope`'s/`TravelSceneScope`'s `if (standalone)` blocks (same reasoning as `IAudioSettingsService` there), since each scene's bootstrapper (below) resolves `IAudioManager` directly.

## BGM wiring

| Scene | Hook | Call |
|---|---|---|
| Planet | `App/PlanetSceneScope.cs` → `PlanetSceneBootstrapper.Start()`, after `_planetController.Load(_startPlanet)` | `_audio.PlayBgmForPlanet(_startPlanet)` |
| SolarSystem | `App/SolarSystemScope.cs` → `SolarSystemBootstrapper.Start()`, near the top | `_audio.PlaySolarSystemBgm()` |
| Travel | `App/TravelSceneScope.cs` → `TravelSceneBootstrapper.Start()`, near the top | `_audio.PlayTravelBgm()` |

Each bootstrapper gains an `IAudioManager _audio` constructor parameter (App already references Safety, per Task 3). No stop/fade-out call is needed at scene exit — `CrossfadeTo` in the next scene's bootstrapper handles the transition, and if the same track is requested again (e.g. re-entering the same planet), `CrossfadeTo` no-ops (`_activeBgm.clip == clip` guard) so there's no restart-glitch on repeated visits.

## SFX wiring

**Pattern** (identical at every modal): inject `IAudioManager _audio`, call `_audio.PlaySfx(SfxId.X)` inside the existing button-click handler, alongside the handler's existing logic — one line added per handler, no restructuring. Example (`LandPurchaseModal.cs`):

```csharp
[Inject] private IAudioManager _audio;
...
private void Awake()
{
    _confirmButton.onClick.AddListener(OnConfirmClicked);
    _cancelButton.onClick.AddListener(Close);
    gameObject.SetActive(false);
}
...
public void Open(TileData tile)
{
    ...
    _audio.PlaySfx(SfxId.OpenPanel);
    gameObject.SetActive(true);
}

private void OnConfirmClicked()
{
    if (_currentTile == null) return;
    _audio.PlaySfx(SfxId.Confirm);
    SetBusy(true);
    ...
}

public void Close()
{
    _audio.PlaySfx(SfxId.Cancel);
    _currentTile = null;
    gameObject.SetActive(false);
}
```

Full hook table:

| SFX | Hook |
|---|---|
| `Confirm` | `LandPurchaseModal.OnConfirmClicked`, `SettingsPanel.OnLogoutConfirmed` (Yes), `DisplayNameModal.OnConfirmClicked`, `EmailVerificationModal` confirm handler |
| `Cancel` | `LandPurchaseModal.Close`, `SettingsPanel.Close` / logout-No, `DisplayNameModal.Close`, `TileInfoModal.Close`, `EmailVerificationModal.Close` |
| `OpenPanel` | Every modal's `Open()`: `LandPurchaseModal`, `TileInfoModal`, `SettingsPanel`, `DisplayNameModal`, `AvatarSelectionModal`, `EmailVerificationModal` |
| `MiningComplete` | `Mining/MiningController.ClaimIdleSessionAsync`, right after `session.Claim()` |
| `ActiveMiningTap` | `UI/ActiveMiningMinigameView.OnTapped(hitTarget: true)`, alongside `SpawnHitVfx()` |
| `NewMessage` | New subscriber on `Social/ChatChannelController.ChatMessageReceivedEvent` (App-layer, e.g. `SocialServicesInitializer` or a small new listener — `Social.asmdef` doesn't reference `Safety`, so this lives in `App`, which already references both) |
| `TravelConfirm` | `UI/PlanetPreviewPanel.OnLaunchClicked` |
| `PlanetObserveConfirm` | `Travel/SkyDiscoveryController.cs:236`, right at `EventBus.Publish(new TravelPreviewRequestedEvent { Planet = _locked });` (the Sky Discovery lock-on moment) |
| `CoinsReward` | `Mining/MiningController.ClaimIdleSessionAsync` coin-grant path; `TileInfoModal.OnTileYieldClaimCompleted` on success |
| `AsteroidDestroyed` | `Mining/MiningController.cs` — after each of the three `asteroid.Mine(asteroid.RemainingYield)` calls (lines 73, 150, 174), fire only if `asteroid.IsDepleted` is now true |
| `RocketDepart` | `Travel/TravelLoadingController.OnTakeOffRequested` → `PlayTakeOff()` |
| `RocketArrive` | `Travel/TravelLoadingController.OnLandRequested` → `PlayLand()` |

`Mining.asmdef`/`Travel.asmdef` currently don't reference `Safety` — both need that reference added (same move Task 3 made for `App`/`UI`).

## Data Flow

Scene loads → that scene's bootstrapper resolves `IAudioManager` and calls the matching `PlayXBgm`/`PlayBgmForPlanet` → `AudioManager` crossfades from whatever was playing (or starts fresh silence→clip on the very first call, since `_activeBgm.clip` is null initially so the guard doesn't block it). A UI action or gameplay event fires → the owning component calls `PlaySfx(id)` → catalog lookup → `PlayOneShot` on the shared SFX source, overlapping freely with BGM and other SFX. Every `AudioSource` routes through `AudioConfig.Mixer`'s Music/SFX groups, so `AudioSettingsService`'s existing volume sliders (Task 1) apply without this design touching that code at all.

## Error Handling

- Missing `SfxId` mapping in the catalog: logged via `SULog.Warn`, no-op — never throws, matches the "missing SO reference is a setup bug" convention but degrades silently for individual missing clips rather than crashing (a partially-populated catalog during development shouldn't break gameplay).
- Missing `PlanetDefinition.BgmClip`: falls back to `AudioCatalog.FallbackPlanetBgm` (Saturn/Pluto today; also covers any future planet added without a track yet).
- `AudioConfig.Mixer == null`: `AudioManager` still constructs and plays clips (just unrouted, full volume, no mixer control) rather than throwing — mirrors `AudioSettingsService`'s existing null-mixer tolerance from Task 1.

## Testing

- `Safety/AudioManagerTests.cs`: the catalog-lookup and fallback logic is pure and testable without real playback — `GetSfxClip` returns the right clip / null for unmapped ids (on `AudioCatalog` directly, EditMode, `ScriptableObject.CreateInstance`), and `PlayBgmForPlanet` picks the planet's own clip vs `FallbackPlanetBgm` correctly (can be tested by asserting the resulting `_activeBgm.clip` after construction with a real `AudioListener`-less headless `AudioSource`, which EditMode tests can create via `new GameObject().AddComponent<AudioSource>()` same as `MiningControllerTests` already does for other components).
- No automated coverage for the crossfade timing itself (a `Task.Yield()`-driven coroutine-equivalent) or for any of the ~15 SFX call sites — consistent with this codebase's existing convention that MonoBehaviour UI and gameplay-feedback wiring is manually verified, not unit tested.
- Manual verification: enter Play Mode, confirm BGM starts on Planet/SolarSystem/Travel and crossfades between planets when traveling; tap through each modal confirming Open/Confirm/Cancel SFX; complete an idle-mining claim and an active-mining tap; receive a chat message (mock) and confirm the ping fires once per message, not per re-render.
