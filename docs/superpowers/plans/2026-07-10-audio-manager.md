# Audio Manager (BGM + SFX) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Play background music per scene (Planet: per-planet track; SolarSystem/Travel: dedicated tracks) with crossfade, and play one-shot SFX for UI confirm/cancel/open, mining completion, active-mining taps, new chat messages, and travel/rocket cues — using the project's existing, already-imported first-party audio assets.

**Architecture:** New `Safety/IAudioManager`/`AudioManager` (plain C# singleton, builds its own persistent `DontDestroyOnLoad` GameObject with 2 crossfading BGM `AudioSource`s + 1 SFX `AudioSource`, both routed through Task 1's existing `AudioConfig.Mixer` Music/SFX groups). Data-driven via a new `Config/AudioCatalog` ScriptableObject (SFX catalog + non-planet BGM) and a new `AudioClip BgmClip` field on the existing `PlanetDefinition`. Scene bootstrappers (`PlanetSceneBootstrapper`, `SolarSystemBootstrapper`, `TravelSceneBootstrapper`) trigger BGM; UI modals and gameplay components call `IAudioManager.PlaySfx` directly at their existing interaction points.

**Tech Stack:** Unity 6 (6000.3.12f1), VContainer (DI), NUnit (EditMode tests). Continues on branch `feature/settings-panel` (this is Task 1's audio-preference layer's natural second half).

## Global Constraints

- Namespace must match folder exactly (`Safety/` → `SocialUniverse.Safety`, `Config/` → `SocialUniverse.Config`) — see CLAUDE.md Project Structure table.
- `Core.asmdef` may not reference `Safety`/`Social`/`Net`/`App` — none of this plan's Core-layer code needs to; all SFX/BGM triggers live in `UI`/`Mining`/`Travel`/`App`, which already do or gain a `Safety` reference here.
- `SfxId` lives in `Config`, not `Safety` — `AudioCatalog` (Config) needs `SfxId` and `Safety.asmdef` already references `Config.asmdef` (from Task 1); putting `SfxId` in `Safety` instead would require `Config → Safety`, creating a cycle with the existing `Safety → Config` edge. Do not move it.
- **Lesson from this branch's final review:** any MonoBehaviour with a new `[Inject]` field must have that field's type registered somewhere resolvable in every scope the MonoBehaviour can load under (production parent chain AND each scene's standalone `if (standalone)` block) — a missed registration caused a Critical `NullReferenceException` bug in `SettingsPanel` that only surfaced at runtime. Every task below that adds an `[Inject] IAudioManager` field must be checked against this.
- **Also from that review:** never touch an `[Inject]`-ed field inside `Awake()` — VContainer's `RegisterComponentInHierarchy` injection timing relative to a component's own `Awake()` is not guaranteed in this project (no `DefaultExecutionOrder` anywhere). All `IAudioManager`/`IAudioSettingsService` field access in this plan happens in `Start()` or later (button handlers, `Open()` methods) — never in `Awake()`.
- Volume for BGM/SFX is already fully handled by Task 1's `AudioSettingsService`/Settings-panel sliders — this plan adds no new volume UI and no new `PlayerPrefs` keys.
- No automated tests for MonoBehaviour SFX call sites or the BGM crossfade coroutine — matches this codebase's existing convention (UI/MonoBehaviour wiring is manually verified). Pure catalog-resolution logic (`AudioCatalog.GetSfxClip`, planet-vs-fallback BGM selection) IS unit tested — it has no Unity-runtime side effects.

---

## Task 1: Audio data model + `AudioManager` core (Safety + Config)

**Files:**
- Create: `Assets/_Project/Scripts/Config/SfxId.cs`
- Create: `Assets/_Project/Scripts/Config/AudioCatalog.cs`
- Modify: `Assets/_Project/Scripts/Config/PlanetDefinition.cs`
- Create: `Assets/_Project/Scripts/Safety/IAudioManager.cs`
- Create: `Assets/_Project/Scripts/Safety/AudioManager.cs`
- Create: `Assets/_Project/Tests/EditMode/Safety/AudioManagerTests.cs`

**Interfaces:**
- Produces: `SocialUniverse.Config.SfxId` enum — `Confirm, Cancel, OpenPanel, MiningComplete, ActiveMiningTap, NewMessage, TravelConfirm, PlanetObserveConfirm, CoinsReward, AsteroidDestroyed, RocketDepart, RocketArrive`.
- Produces: `SocialUniverse.Config.AudioCatalog` (ScriptableObject) — `AudioClip SolarSystemBgm`, `AudioClip TravelBgm`, `AudioClip FallbackPlanetBgm`, `AudioClip GetSfxClip(SfxId id)`.
- Produces: `SocialUniverse.Config.PlanetDefinition.BgmClip` (new `AudioClip` property, alongside existing ones).
- Produces: `SocialUniverse.Safety.IAudioManager` — `void PlaySfx(SfxId id)`, `void PlayBgmForPlanet(PlanetDefinition planet)`, `void PlaySolarSystemBgm()`, `void PlayTravelBgm()`.
- Produces: `SocialUniverse.Safety.AudioManager` — constructor `AudioManager(AudioConfig config, AudioCatalog catalog)`; also `public static AudioClip ResolvePlanetBgm(PlanetDefinition planet, AudioCatalog catalog)` (pure, testable).

- [ ] **Step 1: Create `SfxId`**

Create `Assets/_Project/Scripts/Config/SfxId.cs`:

```csharp
namespace SocialUniverse.Config
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

- [ ] **Step 2: Create `AudioCatalog`**

Create `Assets/_Project/Scripts/Config/AudioCatalog.cs`:

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    [System.Serializable]
    public struct SfxEntry
    {
        public SfxId     Id;
        public AudioClip Clip;
    }

    // Data-driven catalog of every clip AudioManager can play, aside from
    // per-planet BGM (which lives on PlanetDefinition.BgmClip itself).
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AudioCatalog", fileName = "AudioCatalog")]
    public class AudioCatalog : ScriptableObject
    {
        [Header("BGM — non-planet scenes")]
        [SerializeField] private AudioClip _solarSystemBgm;
        [SerializeField] private AudioClip _travelBgm;
        [SerializeField] private AudioClip _fallbackPlanetBgm; // planets with no BgmClip of their own (Saturn, Pluto)

        [Header("SFX")]
        [SerializeField] private SfxEntry[] _sfxEntries;

        public AudioClip SolarSystemBgm    => _solarSystemBgm;
        public AudioClip TravelBgm         => _travelBgm;
        public AudioClip FallbackPlanetBgm => _fallbackPlanetBgm;

        public AudioClip GetSfxClip(SfxId id)
        {
            foreach (var entry in _sfxEntries)
                if (entry.Id == id) return entry.Clip;
            return null;
        }
    }
}
```

- [ ] **Step 3: Add `BgmClip` to `PlanetDefinition`**

Edit `Assets/_Project/Scripts/Config/PlanetDefinition.cs`. Add the field after `_orbitDistanceAU` (last existing field):

```csharp
        [SerializeField] private float _orbitDistanceAU = 1f; // approximate real distance from the sun in AU, used only to lay planets out at relatively-correct distances in Sky Discovery
        [SerializeField] private AudioClip _bgmClip;
```

Add the property after `OrbitDistanceAU` (last existing property):

```csharp
        public float               OrbitDistanceAU       => _orbitDistanceAU;
        public AudioClip           BgmClip                => _bgmClip;
```

Add `using UnityEngine;` is already present at the top of this file (line 1) — `AudioClip` is in `UnityEngine`, no new using needed.

- [ ] **Step 4: Create `IAudioManager`**

Create `Assets/_Project/Scripts/Safety/IAudioManager.cs`:

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

- [ ] **Step 5: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/Safety/AudioManagerTests.cs`:

```csharp
using NUnit.Framework;
using SocialUniverse.Config;
using SocialUniverse.Safety;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class AudioManagerTests
    {
        private AudioCatalog     _catalog;
        private PlanetDefinition _planetWithBgm;
        private PlanetDefinition _planetWithoutBgm;
        private AudioClip        _planetClip;
        private AudioClip        _fallbackClip;

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            _planetClip   = AudioClip.Create("planet", 1, 1, 44100, false);
            _fallbackClip = AudioClip.Create("fallback", 1, 1, 44100, false);

            _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            SetField(_catalog, "_fallbackPlanetBgm", _fallbackClip);
            SetField(_catalog, "_sfxEntries", new[]
            {
                new SfxEntry { Id = SfxId.Confirm, Clip = _planetClip }
            });

            _planetWithBgm = ScriptableObject.CreateInstance<PlanetDefinition>();
            SetField(_planetWithBgm, "_bgmClip", _planetClip);

            _planetWithoutBgm = ScriptableObject.CreateInstance<PlanetDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_planetWithBgm);
            Object.DestroyImmediate(_planetWithoutBgm);
            Object.DestroyImmediate(_planetClip);
            Object.DestroyImmediate(_fallbackClip);
        }

        [Test]
        public void ResolvePlanetBgm_returns_planets_own_clip_when_set()
        {
            Assert.AreEqual(_planetClip, AudioManager.ResolvePlanetBgm(_planetWithBgm, _catalog));
        }

        [Test]
        public void ResolvePlanetBgm_falls_back_to_catalog_when_planet_clip_null()
        {
            Assert.AreEqual(_fallbackClip, AudioManager.ResolvePlanetBgm(_planetWithoutBgm, _catalog));
        }

        [Test]
        public void ResolvePlanetBgm_falls_back_when_planet_itself_null()
        {
            Assert.AreEqual(_fallbackClip, AudioManager.ResolvePlanetBgm(null, _catalog));
        }

        [Test]
        public void GetSfxClip_returns_mapped_clip()
        {
            Assert.AreEqual(_planetClip, _catalog.GetSfxClip(SfxId.Confirm));
        }

        [Test]
        public void GetSfxClip_returns_null_for_unmapped_id()
        {
            Assert.IsNull(_catalog.GetSfxClip(SfxId.Cancel));
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they fail to compile**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console`. Expected: compiler error, `AudioManager` does not exist.

- [ ] **Step 7: Implement `AudioManager`**

Create `Assets/_Project/Scripts/Safety/AudioManager.cs`:

```csharp
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using SocialUniverse.Config;
using SocialUniverse.Core;

namespace SocialUniverse.Safety
{
    // Persistent audio playback: BGM with crossfade (two ping-ponged AudioSources),
    // SFX as fire-and-forget one-shots. Volume is entirely the Settings panel's job
    // (AudioSettingsService drives AudioConfig.Mixer's Music/SFX groups) — this class
    // only decides *what* plays *when*, routed through those same groups so slider
    // changes apply immediately to whatever is already playing.
    public class AudioManager : IAudioManager
    {
        private const float CrossfadeSeconds = 1.5f;

        private readonly AudioCatalog _catalog;
        private readonly AudioSource  _bgmA;
        private readonly AudioSource  _bgmB;
        private readonly AudioSource  _sfx;
        private          AudioSource  _activeBgm;

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

        public void PlayBgmForPlanet(PlanetDefinition planet) => CrossfadeTo(ResolvePlanetBgm(planet, _catalog));
        public void PlaySolarSystemBgm()                      => CrossfadeTo(_catalog.SolarSystemBgm);
        public void PlayTravelBgm()                           => CrossfadeTo(_catalog.TravelBgm);

        public static AudioClip ResolvePlanetBgm(PlanetDefinition planet, AudioCatalog catalog) =>
            planet != null && planet.BgmClip != null ? planet.BgmClip : catalog.FallbackPlanetBgm;

        private void CrossfadeTo(AudioClip clip)
        {
            if (clip == null || _activeBgm.clip == clip) return;

            var incoming = _activeBgm == _bgmA ? _bgmB : _bgmA;
            var outgoing = _activeBgm;

            incoming.clip   = clip;
            incoming.volume = 0f;
            incoming.Play();
            _activeBgm = incoming;

            _ = FadeAsync(outgoing, incoming);
        }

        private static async Task FadeAsync(AudioSource outgoing, AudioSource incoming)
        {
            float t = 0f;
            while (t < CrossfadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / CrossfadeSeconds);
                outgoing.volume = 1f - p;
                incoming.volume = p;
                await Task.Yield();
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

- [ ] **Step 8: Run tests and confirm they pass**

Use `mcp__UnityMCP__run_tests` (EditMode), then `mcp__UnityMCP__get_test_job` with `wait_timeout: 60`. Expected: all 5 new `AudioManagerTests` pass, full suite still green (baseline was 202 before this task — see ledger for the current true baseline before starting).

- [ ] **Step 9: Commit**

```bash
git add Assets/_Project/Scripts/Config/SfxId.cs Assets/_Project/Scripts/Config/SfxId.cs.meta Assets/_Project/Scripts/Config/AudioCatalog.cs Assets/_Project/Scripts/Config/AudioCatalog.cs.meta Assets/_Project/Scripts/Config/PlanetDefinition.cs Assets/_Project/Scripts/Safety/IAudioManager.cs Assets/_Project/Scripts/Safety/IAudioManager.cs.meta Assets/_Project/Scripts/Safety/AudioManager.cs Assets/_Project/Scripts/Safety/AudioManager.cs.meta Assets/_Project/Tests/EditMode/Safety/AudioManagerTests.cs
git commit -m "$(cat <<'EOF'
Add AudioCatalog, PlanetDefinition.BgmClip, and AudioManager core

EOF
)"
```

(Check `git status` first and include any other `.meta` files Unity generated.)

---

## Task 2: DI registration + BGM scene wiring

**Files:**
- Modify: `Assets/_Project/Scripts/App/RootLifetimeScope.cs`
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs`
- Modify: `Assets/_Project/Scripts/App/SolarSystemScope.cs`
- Modify: `Assets/_Project/Scripts/App/TravelSceneScope.cs`

**Interfaces:**
- Consumes: `SocialUniverse.Safety.IAudioManager`/`AudioManager` (Task 1), `SocialUniverse.Config.AudioCatalog` (Task 1).
- Produces: `IAudioManager` resolvable in every scope this plan's later tasks inject it into (production via `RootLifetimeScope`; standalone Planet/SolarSystem/Travel via their own `if (standalone)` blocks).

No automated tests — DI registration and bootstrapper scene-load calls are verified by compile-clean + full suite still passing + the Task 6 manual smoke test, same convention as the settings-panel branch's `LogoutState`/`SettingsPanel` DI work.

- [ ] **Step 1: Register `AudioCatalog`/`AudioManager` in `RootLifetimeScope`**

Edit `Assets/_Project/Scripts/App/RootLifetimeScope.cs`. Add the field near `_audioConfig`:

```csharp
        [SerializeField] private AudioConfig  _audioConfig;
        [SerializeField] private AudioCatalog _audioCatalog;
```

Insert right after the existing `builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();` line:

```csharp
            builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();

            builder.RegisterInstance(_audioCatalog);
            builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
```

- [ ] **Step 2: Register `AudioCatalog`/`AudioManager` in `PlanetSceneScope`'s standalone block**

Edit `Assets/_Project/Scripts/App/PlanetSceneScope.cs`. Add the field near `_audioConfig`:

```csharp
        [SerializeField] private AudioConfig  _audioConfig;    // used in standalone mode only; production gets it from RootLifetimeScope
        [SerializeField] private AudioCatalog _audioCatalog;   // used in standalone mode only; production gets it from RootLifetimeScope
```

Insert inside the `if (standalone) { ... }` block, right after the existing Audio settings registration (`builder.Register<AudioSettingsService>...`):

```csharp
                builder.RegisterInstance(_audioConfig != null ? _audioConfig : ScriptableObject.CreateInstance<AudioConfig>());
                builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();

                builder.RegisterInstance(_audioCatalog != null ? _audioCatalog : ScriptableObject.CreateInstance<AudioCatalog>());
                builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
```

- [ ] **Step 3: Register `AudioCatalog`/`AudioManager` in `SolarSystemScope`'s standalone block**

Edit `Assets/_Project/Scripts/App/SolarSystemScope.cs`. Add fields:

```csharp
        [SerializeField] private EconomyConfig    _economyConfig;
        [SerializeField] private DatabaseRegistry _databaseRegistry;
        [SerializeField] private TravelTimeTable  _travelTimeTable;
        [SerializeField] private AudioConfig      _audioConfig;   // standalone-mode fallback, mirrors PlanetSceneScope
        [SerializeField] private AudioCatalog     _audioCatalog;  // standalone-mode fallback, mirrors PlanetSceneScope
```

Add a `using SocialUniverse.Safety;` alongside the existing usings at the top. Inside the `if (parentReference.Type == null) { ... }` block (currently registers `SceneLoader`/`LocalMockBackendClient`/`ServerTime`), add:

```csharp
                builder.RegisterInstance(_audioConfig != null ? _audioConfig : ScriptableObject.CreateInstance<AudioConfig>());
                builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();
                builder.RegisterInstance(_audioCatalog != null ? _audioCatalog : ScriptableObject.CreateInstance<AudioCatalog>());
                builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
```

(`SolarSystemScope` never had an `IAudioSettingsService` standalone registration before this plan — it's being added here for the first time, alongside `IAudioManager`, since `SolarSystemBootstrapper` in Step 5 below will need `IAudioManager`, which for a truly standalone launch needs both. `AudioConfig`/`AudioSettingsService` were only in `PlanetSceneScope`'s standalone block before now.)

- [ ] **Step 4: Register `AudioCatalog`/`AudioManager` in `TravelSceneScope`'s standalone block**

Edit `Assets/_Project/Scripts/App/TravelSceneScope.cs`. Add fields:

```csharp
        [SerializeField] private DatabaseRegistry _databaseRegistry;
        [SerializeField] private AudioConfig      _audioConfig;   // standalone-mode fallback, mirrors PlanetSceneScope
        [SerializeField] private AudioCatalog     _audioCatalog;  // standalone-mode fallback, mirrors PlanetSceneScope
```

Add `using SocialUniverse.Config;` (check if already present — `TravelSceneScope.cs` already has `using SocialUniverse.Config;` per its existing imports) and `using SocialUniverse.Safety;`. Inside the `if (standalone) { ... }` block, add:

```csharp
                builder.RegisterInstance(_audioConfig != null ? _audioConfig : ScriptableObject.CreateInstance<AudioConfig>());
                builder.Register<AudioSettingsService>(Lifetime.Singleton).As<IAudioSettingsService>();
                builder.RegisterInstance(_audioCatalog != null ? _audioCatalog : ScriptableObject.CreateInstance<AudioCatalog>());
                builder.Register<AudioManager>(Lifetime.Singleton).As<IAudioManager>();
```

- [ ] **Step 5: Wire Planet BGM**

Edit `Assets/_Project/Scripts/App/PlanetSceneScope.cs`'s `PlanetSceneBootstrapper` class. Add `IAudioManager audio` to the constructor:

```csharp
        private readonly SceneLoader       _sceneLoader;
        private readonly bool              _standalone;
        private readonly IAudioManager     _audio;

        public PlanetSceneBootstrapper(
            PlanetController  planetController,
            AsteroidSpawner   asteroidSpawner,
            MiningController  miningController,
            DatabaseRegistry  registry,
            PlanetDefinition  startPlanet,
            IEconomyService   economy,
            FuelSystem        fuel,
            ICloudSave        cloudSave,
            LandRegistry      landRegistry,
            HexasphereManager hexasphere,
            TileColorizer     colorizer,
            IAuthService      auth,
            PlayerState       playerState,
            ProfileService    profileService,
            SceneLoader       sceneLoader,
            bool              standalone,
            IAudioManager     audio)
        {
            _planetController = planetController;
            _asteroidSpawner  = asteroidSpawner;
            _miningController = miningController;
            _registry         = registry;
            _startPlanet      = startPlanet;
            _economy          = economy;
            _fuel             = fuel;
            _cloudSave        = cloudSave;
            _landRegistry     = landRegistry;
            _hexasphere       = hexasphere;
            _colorizer        = colorizer;
            _auth             = auth;
            _playerState      = playerState;
            _profileService   = profileService;
            _sceneLoader      = sceneLoader;
            _standalone       = standalone;
            _audio            = audio;
        }
```

In `Start()`, right after `_planetController.Load(_startPlanet);`:

```csharp
            EventBus.Publish(new LoadingStatusEvent(0.15f));
            _planetController.Load(_startPlanet);
            _audio.PlayBgmForPlanet(_startPlanet);
            _asteroidSpawner.SpawnForPlanet(_startPlanet);
```

- [ ] **Step 6: Wire SolarSystem BGM**

Edit `Assets/_Project/Scripts/App/SolarSystemScope.cs`'s `SolarSystemBootstrapper` class:

```csharp
    public class SolarSystemBootstrapper : IStartable
    {
        private readonly FuelSystem      _fuel;
        private readonly IEconomyService _economy;
        private readonly SceneLoader     _sceneLoader;
        private readonly IAudioManager   _audio;

        public SolarSystemBootstrapper(FuelSystem fuel, IEconomyService economy, SceneLoader sceneLoader, IAudioManager audio)
        {
            _fuel        = fuel;
            _economy     = economy;
            _sceneLoader = sceneLoader;
            _audio       = audio;
        }

        public async void Start()
        {
            var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
            if (!ls.IsValid() || !ls.isLoaded)
                await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);

            _audio.PlaySolarSystemBgm();

            EventBus.Publish(new LoadingStatusEvent(0.3f));
```

(Only the constructor and the two new lines change; the rest of `Start()`'s body is unchanged.)

- [ ] **Step 7: Wire Travel BGM**

Edit `Assets/_Project/Scripts/App/TravelSceneScope.cs`'s `TravelSceneBootstrapper` class:

```csharp
    public class TravelSceneBootstrapper : IStartable
    {
        private readonly TravelTripSystem _trips;
        private readonly SceneLoader      _sceneLoader;
        private readonly bool             _standalone;
        private readonly IAudioManager    _audio;

        public TravelSceneBootstrapper(TravelTripSystem trips, SceneLoader sceneLoader, bool standalone, IAudioManager audio)
        {
            _trips       = trips;
            _sceneLoader = sceneLoader;
            _standalone  = standalone;
            _audio       = audio;
        }

        public async void Start()
        {
            if (_standalone)
            {
                var ls = SceneManager.GetSceneByName(Constants.SceneNames.LoadingScreen);
                if (!ls.IsValid() || !ls.isLoaded)
                    await _sceneLoader.LoadAsync(Constants.SceneNames.LoadingScreen);
            }

            _audio.PlayTravelBgm();

            EventBus.Publish(new LoadingStatusEvent(0.5f));
```

(Only the constructor and the two new lines change; the rest of `Start()`'s body is unchanged.)

- [ ] **Step 8: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console` filtered to errors.

- [ ] **Step 9: Run the full EditMode suite to confirm no regressions**

Use `mcp__UnityMCP__run_tests` (EditMode, no filter).

- [ ] **Step 10: Commit**

```bash
git add Assets/_Project/Scripts/App/RootLifetimeScope.cs Assets/_Project/Scripts/App/PlanetSceneScope.cs Assets/_Project/Scripts/App/SolarSystemScope.cs Assets/_Project/Scripts/App/TravelSceneScope.cs
git commit -m "$(cat <<'EOF'
Register AudioManager and wire per-scene BGM (Planet, SolarSystem, Travel)

EOF
)"
```

---

## Task 3: Modal SFX (Confirm / Cancel / OpenPanel)

**Files:**
- Modify: `Assets/_Project/Scripts/UI/LandPurchaseModal.cs`
- Modify: `Assets/_Project/Scripts/UI/TileInfoModal.cs`
- Modify: `Assets/_Project/Scripts/UI/SettingsPanel.cs`
- Modify: `Assets/_Project/Scripts/UI/DisplayNameModal.cs`
- Modify: `Assets/_Project/Scripts/UI/AvatarSelectionModal.cs`
- Modify: `Assets/_Project/Scripts/UI/EmailVerificationModal.cs`

**Interfaces:**
- Consumes: `SocialUniverse.Safety.IAudioManager` (Task 1, registered Task 2). `UI.asmdef` already references `Safety` (from the settings-panel branch's Task 3) — no asmdef change needed.

No automated tests — matches existing convention for MonoBehaviour UI. **Every `IAudioManager` call in this task goes in a method invoked after `Awake()` (a button `onClick`/`onValueChanged` handler, or `Open()`/`Close()`), never inside `Awake()` itself** — see Global Constraints.

- [ ] **Step 1: `LandPurchaseModal`**

Edit `Assets/_Project/Scripts/UI/LandPurchaseModal.cs`. Add the injected field:

```csharp
        [Inject] private Wallet           _wallet;
        [Inject] private PlanetDefinition _planet;
        [Inject] private EconomyConfig    _economyConfig;
        [Inject] private IAudioManager    _audio;
```

Add `using SocialUniverse.Safety;` to the usings.

In `Open(TileData tile)`, right before `gameObject.SetActive(true);`:

```csharp
            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
```

In `Close()`, at the top:

```csharp
        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            _currentTile = null;
            gameObject.SetActive(false);
        }
```

In `OnConfirmClicked()`, right after the null guard:

```csharp
        private void OnConfirmClicked()
        {
            if (_currentTile == null) return;
            _audio.PlaySfx(SfxId.Confirm);
            SetBusy(true);
```

- [ ] **Step 2: `TileInfoModal`**

Edit `Assets/_Project/Scripts/UI/TileInfoModal.cs`. Add field + using (same pattern as Step 1).

In `Open(TileData tile)`, right before `gameObject.SetActive(true);`:

```csharp
            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
```

In `Close()`, at the top:

```csharp
        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            _currentTile = null;
            CancelInvoke(nameof(RefreshYieldEstimate));
            gameObject.SetActive(false);
        }
```

In `OnTileYieldClaimCompleted(TileYieldClaimCompletedEvent e)`, in the success branch:

```csharp
        private void OnTileYieldClaimCompleted(TileYieldClaimCompletedEvent e)
        {
            if (e.Tile != _currentTile) return;

            SetBusy(false);
            if (e.Success)
            {
                _audio.PlaySfx(SfxId.CoinsReward);
                _statusText.text = $"+{e.Granted} coins!";
                RefreshYieldEstimate();
            }
            else
            {
                _statusText.text = $"Claim failed: {e.FailureReason}";
            }
        }
```

- [ ] **Step 3: `SettingsPanel`**

Edit `Assets/_Project/Scripts/UI/SettingsPanel.cs`. Add the field:

```csharp
        [Inject] private IAudioSettingsService _audioSettings;
        [Inject] private GameStateMachine      _fsm;
        [Inject] private IObjectResolver       _resolver;
        [Inject] private IAudioManager         _audio;
```

(Note: `SettingsPanel` already has a field named `_audio` for `IAudioSettingsService` from the settings-panel branch's Task 3 — rename that existing field to `_audioSettings` throughout the file, freeing up `_audio` for the new `IAudioManager` field. The file currently has exactly four `_audio.` references: two in `Start()`, two in `Open()`. All four are shown explicitly below — after this step, `grep -n "_audio\." SettingsPanel.cs` should show only the new `IAudioManager` calls added in this step, plus these four renamed to `_audioSettings.`.)

`Start()` — rename both lines:

```csharp
        private void Start()
        {
            _musicSlider.onValueChanged.AddListener(_audioSettings.SetMusicVolume);
            _sfxSlider.onValueChanged.AddListener(_audioSettings.SetSfxVolume);
        }
```

In `Open()`, rename its two existing lines and add the new one right before `gameObject.SetActive(true);`:

```csharp
        public void Open()
        {
            _musicSlider.SetValueWithoutNotify(_audioSettings.MusicVolume01);
            _sfxSlider.SetValueWithoutNotify(_audioSettings.SfxVolume01);
            _logoutConfirmPanel.SetActive(false);
            _versionText.text = $"v{Application.version}";
            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
        }
```

In `Close()`:

```csharp
        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
        }
```

In `Awake()`, the `_logoutConfirmNo` handler gets a Cancel SFX too:

```csharp
            _logoutConfirmNo.onClick.AddListener(() => { _audio.PlaySfx(SfxId.Cancel); _logoutConfirmPanel.SetActive(false); });
```

In `OnLogoutConfirmed()`, at the top:

```csharp
        private void OnLogoutConfirmed()
        {
            _audio.PlaySfx(SfxId.Confirm);
            SetInteractable(false);
            _fsm.TransitionTo(_resolver.Resolve<LogoutState>());
        }
```

Add `using SocialUniverse.Config;` for `SfxId` (the file already has `using SocialUniverse.Safety;` for `IAudioSettingsService`/`IAudioManager`).

- [ ] **Step 4: `DisplayNameModal`**

Edit `Assets/_Project/Scripts/UI/DisplayNameModal.cs`. Add field:

```csharp
        [Inject] private IAuthService   _auth;
        [Inject] private PlayerState    _playerState;
        [Inject] private ProfileService _profiles;
        [Inject] private SocialConfig   _config;
        [Inject] private IAudioManager  _audio;
```

Add `using SocialUniverse.Safety;` and `using SocialUniverse.Config;` (check `SocialUniverse.Config` isn't already imported — it is, per the file's existing usings, since `SocialConfig` lives there).

In `OnConfirmClicked()`, both success exits get the Confirm SFX — the name-unchanged early return:

```csharp
            if (name == _playerState.DisplayName)
            {
                _audio.PlaySfx(SfxId.Confirm);
                _avatarSelectionModal.UpdateAvatar();
                return;
            }
```

and the name-changed success path:

```csharp
                if (result == null || result.Success)
                {
                    string committed = result?.DisplayName ?? name;
                    _playerState.SetDisplayName(committed);
                    await _auth.UpdateDisplayNameAsync(committed);

                    _avatarSelectionModal.UpdateAvatar();
                    _audio.PlaySfx(SfxId.Confirm);

                    Close();
                }
```

In `Close()`:

```csharp
        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
        }
```

`DisplayNameModal.Open()` does not call `gameObject.SetActive(true)` itself (a pre-existing quirk, not introduced or fixed by this plan — do not add `SetActive(true)` here, out of scope). Add `OpenPanel` SFX at the top of `Open()` anyway, since that part is independent of the activation quirk:

```csharp
        public void Open()
        {
            _audio.PlaySfx(SfxId.OpenPanel);
            _nameInput.text  = _playerState.DisplayName;
            _statusText.text = "";
        }
```

- [ ] **Step 5: `AvatarSelectionModal`**

Edit `Assets/_Project/Scripts/UI/AvatarSelectionModal.cs`. Add field:

```csharp
        [Inject] private PlayerState      _playerState;
        [Inject] private ProfileService   _profiles;
        [Inject] private DatabaseRegistry _registry;
        [Inject] private IAudioManager    _audio;
```

Add `using SocialUniverse.Safety;` and `using SocialUniverse.Config;` (the file already imports `SocialUniverse.Config` for `DatabaseRegistry`).

In `Open()`, right before `gameObject.SetActive(true);`:

```csharp
            var currentAvatar = _registry.AllAvatars.ToList().Find(x => x.AvatarId == _selectedAvatarId);
            _avatarPreview.sprite = currentAvatar.Sprite;
            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
```

No Confirm/Cancel SFX here — this modal has no dedicated confirm/cancel buttons of its own; its commit path is `DisplayNameModal.OnConfirmClicked` calling `UpdateAvatar()` (already covered by Step 4's Confirm SFX).

- [ ] **Step 6: `EmailVerificationModal`**

Edit `Assets/_Project/Scripts/UI/EmailVerificationModal.cs`. Add field:

```csharp
        [Inject] private IAuthService  _auth;
        [Inject] private PlayerState   _playerState;
        [Inject] private IAudioManager _audio;
```

Add `using SocialUniverse.Safety;` and `using SocialUniverse.Config;`.

In `Open()`, right before `gameObject.SetActive(true);`:

```csharp
            _statusText.text = verified ? "Your email is verified." : "";
            _audio.PlaySfx(SfxId.OpenPanel);
            gameObject.SetActive(true);
```

In `Close()`:

```csharp
        public void Close()
        {
            _audio.PlaySfx(SfxId.Cancel);
            gameObject.SetActive(false);
        }
```

No Confirm SFX — `_verifyButton`'s handler can fail and leave the modal open (not a dismiss-and-commit action like the other modals' primary buttons), so it doesn't fit the generic "Confirm" semantic; leaving it unwired is intentional, not a gap.

- [ ] **Step 7: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console` filtered to errors. Pay particular attention to `SettingsPanel.cs` — confirm every pre-existing `_audio.` reference (for `IAudioSettingsService`) was correctly renamed to `_audioSettings.` and none were missed (a missed rename would silently call the wrong interface's members and fail to compile, since `IAudioSettingsService` has no `PlaySfx` method).

- [ ] **Step 8: Run the full EditMode suite to confirm no regressions**

Use `mcp__UnityMCP__run_tests` (EditMode, no filter).

- [ ] **Step 9: Commit**

```bash
git add Assets/_Project/Scripts/UI/LandPurchaseModal.cs Assets/_Project/Scripts/UI/TileInfoModal.cs Assets/_Project/Scripts/UI/SettingsPanel.cs Assets/_Project/Scripts/UI/DisplayNameModal.cs Assets/_Project/Scripts/UI/AvatarSelectionModal.cs Assets/_Project/Scripts/UI/EmailVerificationModal.cs
git commit -m "$(cat <<'EOF'
Wire Confirm/Cancel/OpenPanel SFX into modal panels

EOF
)"
```

---

## Task 4: Mining SFX

**Files:**
- Modify: `Assets/_Project/Scripts/Mining/SocialUniverse.Mining.asmdef`
- Modify: `Assets/_Project/Scripts/Mining/MiningController.cs`
- Modify: `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`

**Interfaces:**
- Consumes: `SocialUniverse.Safety.IAudioManager` (Task 1, registered Task 2).
- `Mining.asmdef` does not currently reference `Safety` — this task adds that reference.

No automated tests for the SFX calls themselves (consistent with the rest of this plan). `MiningController` is a plain C# class (not a MonoBehaviour) constructor-injected by VContainer — adding a new constructor parameter is compile-checked, not a DI-registration risk like `[Inject]` fields on MonoBehaviours.

- [ ] **Step 1: Add `Safety` reference to `Mining.asmdef`**

Edit `Assets/_Project/Scripts/Mining/SocialUniverse.Mining.asmdef`:

```json
    "references": [
        "VContainer",
        "SocialUniverse.Core",
        "SocialUniverse.Config",
        "SocialUniverse.World",
        "SocialUniverse.Economy",
        "SocialUniverse.Safety"
    ],
```

- [ ] **Step 2: Wire idle-mining claim SFX in `MiningController`**

Edit `Assets/_Project/Scripts/Mining/MiningController.cs`. Add the constructor parameter:

```csharp
        private readonly IEconomyService        _economy;
        private readonly MiningRewardCalculator  _rewardCalc;
        private readonly AsteroidSpawner         _spawner;
        private readonly EconomyConfig           _config;
        private readonly PlanetDefinition        _planet;
        private readonly ActiveMiningHandoff     _handoff;
        private readonly IAudioManager           _audio;

        public MiningController(IEconomyService economy, MiningRewardCalculator rewardCalc,
            AsteroidSpawner spawner, EconomyConfig config, PlanetDefinition planet, ActiveMiningHandoff handoff,
            IAudioManager audio)
        {
            _economy    = economy;
            _rewardCalc = rewardCalc;
            _spawner    = spawner;
            _config     = config;
            _planet     = planet;
            _handoff    = handoff;
            _audio      = audio;
        }
```

Add `using SocialUniverse.Safety;` and `using SocialUniverse.Config;` (the file already has `using SocialUniverse.Config;` for `EconomyConfig`/`PlanetDefinition` — check before adding a duplicate).

In `ClaimIdleSessionAsync`, after `session.Claim();`:

```csharp
            var reward = _rewardCalc.Compute(asteroid);
            session.Claim();
            _audio.PlaySfx(SfxId.MiningComplete);

            int mined = asteroid.Mine(asteroid.RemainingYield);
            if (asteroid.IsDepleted) _audio.PlaySfx(SfxId.AsteroidDestroyed);
            int coins = mined * asteroid.Definition.CoinsPerUnit;
```

Inside the `if (coins > 0) { try { ... } }` block, after the successful grant:

```csharp
                try
                {
                    int granted = await _economy.GrantMiningRewardAsync(coins, reward.IdleDurationSeconds, reward.CoinsPerSec);
                    _audio.PlaySfx(SfxId.CoinsReward);
                    SULog.Info($"Idle session claimed: +{mined} {asteroid.Definition.MineralType} -> {granted} coins", SULog.Channel.Mining);
                }
```

- [ ] **Step 3: Wire active-mining tap + result SFX in `ActiveMiningMinigameView`**

Edit `Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs`. Add the field:

```csharp
        [Inject] private ActiveMiningSessionRunner _runner;
        [Inject] private ActiveMiningHandoff       _handoff;
        [Inject] private ActiveMiningState         _activeMiningState;
        [Inject] private IAudioManager             _audio;
```

Add `using SocialUniverse.Safety;` and `using SocialUniverse.Config;`.

In `OnTapped(bool hitTarget)`, in the `hitTarget` branch:

```csharp
            if (hitTarget)
            {
                _audio.PlaySfx(SfxId.ActiveMiningTap);
                SpawnHitVfx();
                _runner.Session.RegisterHit();
            }
```

In `ShowResult(ActiveMiningStage stage)`, in the `succeeded` branch:

```csharp
        private void ShowResult(ActiveMiningStage stage)
        {
            bool succeeded = stage == ActiveMiningStage.Success;

            if (succeeded)
            {
                _audio.PlaySfx(SfxId.MiningComplete);
                _audio.PlaySfx(SfxId.AsteroidDestroyed);
            }

            if (_resultBanner != null) _resultBanner.SetActive(true);
            if (_resultText   != null) _resultText.text = succeeded ? "Success!" : "Failed";
```

(`AsteroidDestroyed` fires unconditionally on `succeeded` here — active mining always fully depletes the asteroid on success, per `MiningController.CompleteActiveMiningAsync`'s unconditional `asteroid.Mine(asteroid.RemainingYield)` call, so there's no need to check `IsDepleted` the way the idle-mining path does.)

- [ ] **Step 4: Update `MiningControllerTests` for the new constructor parameter**

`Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs` constructs `MiningController` directly at two call sites (lines 73 and 307) using the old 6-argument constructor — both will fail to compile once Step 2 adds a 7th `IAudioManager audio` parameter. Add a minimal fake near the existing `ThrowingEconomyService` fake (same file, `namespace SocialUniverse.Tests`):

```csharp
    public class FakeAudioManager : IAudioManager
    {
        public void PlaySfx(SfxId id) { }
        public void PlayBgmForPlanet(PlanetDefinition planet) { }
        public void PlaySolarSystemBgm() { }
        public void PlayTravelBgm() { }
    }
```

Add `using SocialUniverse.Safety;` to the file's usings (it already has `using SocialUniverse.Config;`).

Update both call sites:

```csharp
            _mining = new MiningController(_economy, _rewardCalc, _spawner, _config, _planet, _handoff, new FakeAudioManager());
```

```csharp
            var mining = new MiningController(throwingEconomy, _rewardCalc, _spawner, _config, _planet, _handoff, new FakeAudioManager());
```

- [ ] **Step 5: Verify compilation and run the full EditMode suite to confirm no regressions**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console` filtered to errors, then `mcp__UnityMCP__run_tests` (EditMode, no filter).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Mining/SocialUniverse.Mining.asmdef Assets/_Project/Scripts/Mining/MiningController.cs Assets/_Project/Scripts/UI/ActiveMiningMinigameView.cs Assets/_Project/Tests/EditMode/Mining/MiningControllerTests.cs
git commit -m "$(cat <<'EOF'
Wire mining-complete, active-mining-tap, asteroid-destroyed, and coins SFX

EOF
)"
```

---

## Task 5: Travel SFX + global EventBus audio bridge

**Files:**
- Modify: `Assets/_Project/Scripts/Travel/SocialUniverse.Travel.asmdef`
- Modify: `Assets/_Project/Scripts/Travel/SkyDiscoveryController.cs`
- Modify: `Assets/_Project/Scripts/UI/PlanetPreviewPanel.cs`
- Create: `Assets/_Project/Scripts/App/AudioEventBridge.cs`
- Modify: `Assets/_Project/Scripts/App/RootLifetimeScope.cs`

**Interfaces:**
- Consumes: `SocialUniverse.Safety.IAudioManager` (Task 1). `SocialUniverse.Social.ChatChannelController.ChatMessageReceivedEvent` (existing), `SocialUniverse.Core.TravelLoadingTakeOffRequestedEvent`/`TravelLoadingLandRequestedEvent` (existing, in `Core/TravelLoadingEvents.cs`).
- `Travel.asmdef` does not currently reference `Safety` — this task adds it.

**Why a new `AudioEventBridge` instead of adding `[Inject] IAudioManager` to `TravelLoadingController`:** `TravelLoadingController` (in the `TravelLoading` scene) has zero `[Inject]` fields today and no `LifetimeScope` registers it anywhere — it's a pure EventBus-reactive `MonoBehaviour`, not VContainer-managed at all. Giving it DI would require building a whole new scene scope, well beyond this task's scope. Since `TravelLoadingTakeOffRequestedEvent`/`TravelLoadingLandRequestedEvent` are published on the global `EventBus` (not scene-scoped), a Root-scoped, already-DI-wired listener can react to them from anywhere — exactly the same shape `SocialServicesInitializer` already uses for `PlayerReadyEvent`/`PlayerLoggedOutEvent`. `ChatMessageReceivedEvent` is bundled into the same bridge for the same reason (global EventBus event, no natural per-message UI component to own the SFX call).

No automated tests — EventBus subscription wiring for a fire-and-forget SFX call, consistent with the rest of this plan.

- [ ] **Step 1: Add `Safety` reference to `Travel.asmdef`**

Edit `Assets/_Project/Scripts/Travel/SocialUniverse.Travel.asmdef`:

```json
    "references": [
        "VContainer",
        "SocialUniverse.Core",
        "SocialUniverse.Config",
        "SocialUniverse.Economy",
        "SocialUniverse.Progression",
        "SocialUniverse.Safety",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "Unity.Cinemachine"
    ],
```

- [ ] **Step 2: Wire `PlanetObserveConfirm` in `SkyDiscoveryController`**

Read `Assets/_Project/Scripts/Travel/SkyDiscoveryController.cs` first to find its existing `[Inject]` fields (it's already `RegisterComponentInHierarchy`-registered in `SolarSystemScope.cs`, so DI is already wired for it — no new registration needed). Add:

```csharp
[Inject] private IAudioManager _audio;
```

alongside its existing injected fields, and add `using SocialUniverse.Safety;` + `using SocialUniverse.Config;` to its usings.

At line 236 (`EventBus.Publish(new TravelPreviewRequestedEvent { Planet = _locked });`), add immediately before it:

```csharp
            _audio.PlaySfx(SfxId.PlanetObserveConfirm);
            EventBus.Publish(new TravelPreviewRequestedEvent { Planet = _locked });
```

- [ ] **Step 3: Wire `TravelConfirm` in `PlanetPreviewPanel`**

Edit `Assets/_Project/Scripts/UI/PlanetPreviewPanel.cs`. Add the field:

```csharp
        [Inject] private TravelService _travelService;
        [Inject] private PlayerState   _playerState;
        [Inject] private IAudioManager _audio;
```

Add `using SocialUniverse.Safety;` and `using SocialUniverse.Config;`.

Edit `OnLaunchClicked()`:

```csharp
        private void OnLaunchClicked()
        {
            if (_pending == null) return;
            _audio.PlaySfx(SfxId.TravelConfirm);
            EventBus.Publish(new TravelConfirmedEvent { Planet = _pending });
            // Close();
        }
```

- [ ] **Step 4: Create `AudioEventBridge`**

Create `Assets/_Project/Scripts/App/AudioEventBridge.cs`:

```csharp
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Safety;
using SocialUniverse.Social;

namespace SocialUniverse.App
{
    // Plays SFX in response to global EventBus events that have no single
    // natural DI-wired UI component to own the call — chat messages (any
    // scene) and rocket takeoff/landing (the TravelLoading scene, which has
    // no LifetimeScope of its own — see plan Task 5 for why). Lives in the
    // Root scope so it's alive for every scene these events can fire in,
    // same lifetime shape as SocialServicesInitializer.
    public class AudioEventBridge : IStartable, System.IDisposable
    {
        private readonly IAudioManager _audio;

        public AudioEventBridge(IAudioManager audio)
        {
            _audio = audio;
        }

        public void Start()
        {
            EventBus.Subscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            EventBus.Subscribe<TravelLoadingTakeOffRequestedEvent>(OnTakeOffRequested);
            EventBus.Subscribe<TravelLoadingLandRequestedEvent>(OnLandRequested);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            EventBus.Unsubscribe<TravelLoadingTakeOffRequestedEvent>(OnTakeOffRequested);
            EventBus.Unsubscribe<TravelLoadingLandRequestedEvent>(OnLandRequested);
        }

        private void OnChatMessageReceived(ChatChannelController.ChatMessageReceivedEvent e)
        {
            if (e.Message.FromSelf) return; // no ping for your own outgoing message
            _audio.PlaySfx(SfxId.NewMessage);
        }

        private void OnTakeOffRequested(TravelLoadingTakeOffRequestedEvent e) => _audio.PlaySfx(SfxId.RocketDepart);
        private void OnLandRequested(TravelLoadingLandRequestedEvent e)       => _audio.PlaySfx(SfxId.RocketArrive);
    }
}
```

`TravelLoadingTakeOffRequestedEvent`/`TravelLoadingLandRequestedEvent` (`Core/TravelLoadingEvents.cs`) are plain classes each carrying a single `PlanetDefinition Planet` field — confirmed exact shape; the handlers above don't need to read `.Planet` at all, so no adjustment is needed beyond the code shown.

- [ ] **Step 5: Register `AudioEventBridge`**

Edit `Assets/_Project/Scripts/App/RootLifetimeScope.cs`. Add, near `SocialServicesInitializer`'s registration:

```csharp
            builder.RegisterEntryPoint<SocialServicesInitializer>();
            builder.RegisterEntryPoint<AudioEventBridge>();
```

- [ ] **Step 6: Verify compilation**

Use `mcp__UnityMCP__refresh_unity` then `mcp__UnityMCP__read_console` filtered to errors.

- [ ] **Step 7: Run the full EditMode suite to confirm no regressions**

Use `mcp__UnityMCP__run_tests` (EditMode, no filter).

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Travel/SocialUniverse.Travel.asmdef Assets/_Project/Scripts/Travel/SkyDiscoveryController.cs Assets/_Project/Scripts/UI/PlanetPreviewPanel.cs Assets/_Project/Scripts/App/AudioEventBridge.cs Assets/_Project/Scripts/App/AudioEventBridge.cs.meta Assets/_Project/Scripts/App/RootLifetimeScope.cs
git commit -m "$(cat <<'EOF'
Wire travel-confirm, planet-observe, rocket, and new-message SFX

EOF
)"
```

---

## Task 6: Unity asset population + manual verification

This task has no C# — it populates the data assets Tasks 1-5's code reads from, using Unity MCP tools, then runs the manual smoke test.

**Files:**
- Create: `Assets/_Project/ScriptableObjects/AudioCatalog.asset`
- Modify: `Assets/_Project/ScriptableObjects/Planets/Planet_Earth.asset` (+7 more planet assets)
- Modify: `Assets/Scenes/Bootstrap.unity` (assign `RootLifetimeScope._audioCatalog`)

- [ ] **Step 1: Create the `AudioCatalog` asset**

Use `mcp__UnityMCP__manage_asset` (or `manage_scriptable_object`, per whichever tool Task 4 of the settings-panel plan found worked — `action=create type_name=SocialUniverse.Config.AudioCatalog`) to create `Assets/_Project/ScriptableObjects/AudioCatalog.asset`. Assign:
- `_solarSystemBgm` → `Assets/Audio/BGM/Social Universe Mvp 2.2.wav`
- `_travelBgm` → `Assets/Audio/BGM/Social_Universe_Mvp_1.wav`
- `_fallbackPlanetBgm` → `Assets/Audio/BGM/Social Universe Mvp 2.2.wav`
- `_sfxEntries`, one entry per `SfxId`:
  - `Confirm` → `Assets/Audio/SFX/403009__inspectorj__ui-confirmation-alert-b3.wav`
  - `Cancel` → `Assets/Audio/SFX/Social Universe UI - return UI.wav`
  - `OpenPanel` → `Assets/Plugins/UltimateCleanGUIPack/Common/Sounds/Open (Button).wav`
  - `MiningComplete` → `Assets/Audio/SFX/Social Universe UI - asteroid claim.wav`
  - `ActiveMiningTap` → `Assets/Audio/SFX/Social_Universe_UI_-_Mining_2.wav`
  - `NewMessage` → `Assets/Audio/SFX/Social Universe UI - ping 2 (play when opening planet chat - once).wav`
  - `TravelConfirm` → `Assets/Audio/SFX/Social Universe UI - confirm 1 (when traveling to another place).wav`
  - `PlanetObserveConfirm` → `Assets/Audio/SFX/Social Universe UI - confirm 2 (when clicking planet to observe).wav`
  - `CoinsReward` → `Assets/Audio/SFX/Social Universe UI - Coins.wav`
  - `AsteroidDestroyed` → `Assets/Audio/SFX/Social Universe UI - Asteroid explosion 1.wav`
  - `RocketDepart` → `Assets/Audio/SFX/Social Universe UI - Rocket Travel 3.wav`
  - `RocketArrive` → `Assets/Audio/SFX/Social Universe UI - Rocket Arrive 1.wav`

- [ ] **Step 2: Assign `BgmClip` on each planet with a track**

Use `mcp__UnityMCP__manage_asset`/`manage_scriptable_object` to set `_bgmClip` on:
- `Planet_Earth.asset` → `LoFI Earth Theme Draft 2.5.wav`
- `Planet_Moon.asset` → `Soc U moon No melody.wav`
- `Planet_Jupiter.asset` → `Social U Jupiter Draft.wav`
- `Planet_Mercury.asset` → `Social U Mercury Draft.wav`
- `Planet_Mars.asset` → `Social U mars Draft.wav`
- `Planet_Venus.asset` → `Social U venus Draft.wav`
- `Planet_Neptune.asset` → `Social_U_Neptune_Draft.wav`
- `Planet_Uranus.asset` → `Social_U_Uranus_Draft.wav`

Leave `Planet_Saturn.asset` and `Planet_Pluto.asset` unassigned — they fall back to `AudioCatalog.FallbackPlanetBgm` per `AudioManager.ResolvePlanetBgm` (Task 1).

- [ ] **Step 3: Assign `RootLifetimeScope._audioCatalog`**

Open the Bootstrap scene, locate the `RootLifetimeScope` component (same GameObject as `_audioConfig` was assigned to in the settings-panel branch's Task 4), assign `_audioCatalog` → `AudioCatalog.asset`.

- [ ] **Step 4: Verify compilation and save**

Use `mcp__UnityMCP__read_console` filtered to errors. Save the Bootstrap scene via `mcp__UnityMCP__manage_scene`.

- [ ] **Step 5: Run the full EditMode suite one more time**

Use `mcp__UnityMCP__run_tests` (EditMode, no filter). Expected: same pass count as Task 5's end state — this task touched no test-covered code.

- [ ] **Step 6: Manual Play Mode smoke test**

Enter Play Mode (`_devMode` on) and verify:
1. Planet scene BGM starts and matches the current planet (Earth's LoFi track on first login).
2. Traveling to a different planet crossfades to that planet's track (or the fallback for Saturn/Pluto) once landed; SolarSystem scene plays the Mvp 2.2 track; Travel scene plays the Mvp 1 track during transit.
3. Opening any modal (Settings, Land Purchase, Tile Info, Display Name, Avatar Selection, Email Verification) plays the Open-panel sound; Confirm/Cancel buttons play their respective sounds.
4. Completing an idle-mining claim plays the claim/coins/asteroid-destroyed sounds; tapping during active mining plays a tap sound per hit, and a success round plays the completion sounds.
5. Receiving a chat message (not your own) plays the ping sound once per message.
6. Starting a trip plays the travel-confirm sound; locking onto a planet in Sky Discovery plays the observe sound; the TravelLoading scene's takeoff/landing legs play the rocket sounds.
7. Confirm all of the above scales with the Settings panel's Music/SFX sliders (drag each to 0, confirm the corresponding category goes silent).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/ScriptableObjects/AudioCatalog.asset Assets/_Project/ScriptableObjects/AudioCatalog.asset.meta Assets/_Project/ScriptableObjects/Planets Assets/Scenes/Bootstrap.unity
git commit -m "$(cat <<'EOF'
Populate AudioCatalog and per-planet BGM clips

EOF
)"
```

(Run `git status` first and include any other files Unity touched.)
