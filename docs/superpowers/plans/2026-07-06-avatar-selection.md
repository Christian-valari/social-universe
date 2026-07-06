# Avatar Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let players pick a 2D profile avatar from 25 pre-existing character sprites; new signups get a random one automatically, and it's server-persisted and editable later from the Planet-scene HUD.

**Architecture:** Follows the existing display-name feature's shape exactly: a `PlayerProfile.AvatarId` field round-trips through the existing `ProfileService`/`UpdateProfile` Cloud Code function, `PlayerState.AvatarId` mirrors `DisplayName`, and a new `AvatarSelectionModal` mirrors `DisplayNameModal`. A new `AvatarDefinition` ScriptableObject (one per sprite, registered on `DatabaseRegistry`) supplies the catalog. A small pure helper, `AvatarAssignment.ResolveAvatarId`, isolates the "assign a random one if the profile doesn't have one yet" decision so it's unit-testable without the rest of `PlanetSceneBootstrapper`'s heavy dependency graph.

**Tech Stack:** Unity 6 (URP), VContainer DI, NUnit (Unity Test Framework, EditMode), Unity Cloud Code (Cloud Save).

## Global Constraints

- Self-only scope: the avatar shows in the local player's own HUD/picker only. `IPresenceService`/`PresencePlayer` is not touched.
- 25 pickable avatars (all sprites in `Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/` except `Empty.png` and `Empty Gray.png`). The exact filename → asset name → id mapping (must stay in sync between the 25 `AvatarDefinition` assets and `ServerCode/UpdateProfile.js`'s `AVATAR_IDS` list):

  | Sprite file | Asset name | `AvatarId` |
  |---|---|---|
  | `Alien Blue.png` | `Avatar_AlienBlue` | `avatar_alien_blue` |
  | `Alien Green.png` | `Avatar_AlienGreen` | `avatar_alien_green` |
  | `Boy1.png` | `Avatar_Boy1` | `avatar_boy1` |
  | `Boy 1 Dark.png` | `Avatar_Boy1Dark` | `avatar_boy_1_dark` |
  | `Boy 2.png` | `Avatar_Boy2` | `avatar_boy_2` |
  | `Boy 3.png` | `Avatar_Boy3` | `avatar_boy_3` |
  | `Boy 4.png` | `Avatar_Boy4` | `avatar_boy_4` |
  | `Boy 5.png` | `Avatar_Boy5` | `avatar_boy_5` |
  | `Boy 6.png` | `Avatar_Boy6` | `avatar_boy_6` |
  | `Boy 6 Light.png` | `Avatar_Boy6Light` | `avatar_boy_6_light` |
  | `Boy 7.png` | `Avatar_Boy7` | `avatar_boy_7` |
  | `Boy 8.png` | `Avatar_Boy8` | `avatar_boy_8` |
  | `Boy 9.png` | `Avatar_Boy9` | `avatar_boy_9` |
  | `Boy 10.png` | `Avatar_Boy10` | `avatar_boy_10` |
  | `Dark.png` | `Avatar_Dark` | `avatar_dark` |
  | `Girl 1.png` | `Avatar_Girl1` | `avatar_girl_1` |
  | `Girl 2.png` | `Avatar_Girl2` | `avatar_girl_2` |
  | `Girl 2 Dark.png` | `Avatar_Girl2Dark` | `avatar_girl_2_dark` |
  | `Girl 3.png` | `Avatar_Girl3` | `avatar_girl_3` |
  | `Girl 4.png` | `Avatar_Girl4` | `avatar_girl_4` |
  | `Girl 5.png` | `Avatar_Girl5` | `avatar_girl_5` |
  | `Girl 6.png` | `Avatar_Girl6` | `avatar_girl_6` |
  | `Girl 7.png` | `Avatar_Girl7` | `avatar_girl_7` |
  | `Girl 8.png` | `Avatar_Girl8` | `avatar_girl_8` |
  | `Wizard.png` | `Avatar_Wizard` | `avatar_wizard` |

- Namespace/assembly placement per `CLAUDE.md`: `AvatarDefinition` → `SocialUniverse.Config`; `AvatarAssignment` → `SocialUniverse.Progression`; `AvatarSelectionModal` → `SocialUniverse.UI`.
- No client-minted state: the avatar choice is only ever considered "committed" after `ProfileService.UpdateAvatarAsync` succeeds (or the `result == null` mock-backend convention already used by `DisplayNameModal`).

---

### Task 1: `AvatarDefinition` ScriptableObject

**Files:**
- Create: `Assets/_Project/Scripts/Config/AvatarDefinition.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `AvatarDefinition` class with `string AvatarId` and `Sprite Sprite` read-only properties, backed by `[SerializeField]` fields `_avatarId`/`_sprite`. Used by Task 2 (`DatabaseRegistry`), Task 9 (`AvatarSelectionModal`), Task 10 (`HUDController`).

- [ ] **Step 1: Write the class**

```csharp
using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AvatarDefinition", fileName = "NewAvatar")]
    public class AvatarDefinition : ScriptableObject
    {
        [SerializeField] private string _avatarId;
        [SerializeField] private Sprite _sprite;

        public string AvatarId => _avatarId;
        public Sprite Sprite   => _sprite;
    }
}
```

- [ ] **Step 2: Let Unity compile and confirm no errors**

This is a plain data class (mirrors `DroneDefinition`/`AsteroidDefinition`, neither of which has a dedicated unit test) — no automated test for this file. Use the Unity Editor's Console (or `mcp__UnityMCP__read_console`) to confirm the new script compiles cleanly before moving on.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Config/AvatarDefinition.cs
git commit -m "config: add AvatarDefinition ScriptableObject for the avatar catalog"
```

---

### Task 2: `DatabaseRegistry` avatar catalog

**Files:**
- Modify: `Assets/_Project/Scripts/Config/DatabaseRegistry.cs`
- Test: `Assets/_Project/Tests/EditMode/Config/DatabaseRegistryAvatarTests.cs` (new)

**Interfaces:**
- Consumes: `AvatarDefinition` (Task 1).
- Produces: `DatabaseRegistry.AllAvatars` (`IEnumerable<AvatarDefinition>`) and `DatabaseRegistry.GetAvatar(string avatarId)` (`AvatarDefinition`, null if not found). Used by Task 7 (hydration), Task 9 (`AvatarSelectionModal`), Task 10 (`HUDController`).

- [ ] **Step 1: Write the failing test**

```csharp
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Config;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class DatabaseRegistryAvatarTests
    {
        private static AvatarDefinition MakeAvatar(string avatarId)
        {
            var def = ScriptableObject.CreateInstance<AvatarDefinition>();
            typeof(AvatarDefinition).GetField("_avatarId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, avatarId);
            return def;
        }

        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [Test]
        public void AllAvatars_returns_every_registered_avatar()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            var avatars  = new[] { MakeAvatar("avatar_a"), MakeAvatar("avatar_b") };
            SetField(registry, "_avatars", avatars);

            Assert.AreEqual(2, registry.AllAvatars.Count());
        }

        [Test]
        public void GetAvatar_finds_by_id()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            var avatars  = new[] { MakeAvatar("avatar_a"), MakeAvatar("avatar_b") };
            SetField(registry, "_avatars", avatars);

            var found = registry.GetAvatar("avatar_b");

            Assert.IsNotNull(found);
            Assert.AreEqual("avatar_b", found.AvatarId);
        }

        [Test]
        public void GetAvatar_returns_null_for_unknown_id()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            SetField(registry, "_avatars", new[] { MakeAvatar("avatar_a") });

            Assert.IsNull(registry.GetAvatar("avatar_nonexistent"));
        }

        [Test]
        public void AllAvatars_is_empty_not_null_when_unset()
        {
            var registry = ScriptableObject.CreateInstance<DatabaseRegistry>();

            Assert.IsNotNull(registry.AllAvatars);
            Assert.AreEqual(0, registry.AllAvatars.Count());
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run via Unity Test Runner (EditMode) or:
```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -testFilter DatabaseRegistryAvatarTests
```
Expected: FAIL — `DatabaseRegistry` has no `AllAvatars`/`GetAvatar`/`_avatars` yet (compile error).

- [ ] **Step 3: Extend `DatabaseRegistry`**

In `Assets/_Project/Scripts/Config/DatabaseRegistry.cs`, add a field alongside the existing three, and two members alongside the existing `AllDrones`/`GetDrone`:

```csharp
        [SerializeField] private PlanetDefinition[]   _planets;
        [SerializeField] private AsteroidDefinition[] _asteroids;
        [SerializeField] private DroneDefinition[]    _drones;
        [SerializeField] private ItemDefinition[]     _items;
        [SerializeField] private AvatarDefinition[]   _avatars;

        public IEnumerable<PlanetDefinition>   AllPlanets   => _planets   ?? Array.Empty<PlanetDefinition>();
        public IEnumerable<AsteroidDefinition> AllAsteroids => _asteroids ?? Array.Empty<AsteroidDefinition>();
        public IEnumerable<DroneDefinition>    AllDrones    => _drones    ?? Array.Empty<DroneDefinition>();
        public IEnumerable<ItemDefinition>     AllItems     => _items     ?? Array.Empty<ItemDefinition>();
        public IEnumerable<AvatarDefinition>   AllAvatars   => _avatars   ?? Array.Empty<AvatarDefinition>();

        public PlanetDefinition   GetPlanet(string id)          => Array.Find(_planets,   p => p.PlanetId     == id);
        public AsteroidDefinition GetAsteroid(string mineral)   => Array.Find(_asteroids, a => a.MineralType  == mineral);
        public DroneDefinition    GetDrone(string droneId)      => Array.Find(_drones,    d => d.DroneId      == droneId);
        public ItemDefinition     GetItem(string itemId)        => Array.Find(_items,     i => i.ItemId       == itemId);
        public AvatarDefinition   GetAvatar(string avatarId)    => Array.Find(_avatars,   a => a.AvatarId      == avatarId);
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2. Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Config/DatabaseRegistry.cs Assets/_Project/Tests/EditMode/Config/DatabaseRegistryAvatarTests.cs
git commit -m "config: add avatar catalog to DatabaseRegistry"
```

---

### Task 3: `PlayerProfile.AvatarId` + `ProfileService.UpdateAvatarAsync`

**Files:**
- Modify: `Assets/_Project/Scripts/Social/PlayerProfile.cs`
- Modify: `Assets/_Project/Scripts/Social/ProfileService.cs`
- Test: `Assets/_Project/Tests/EditMode/Social/ProfileServiceTests.cs` (extend)

**Interfaces:**
- Consumes: `IBackendClient.CallAsync<T>` (existing), `ProfileUpdateResult` (existing).
- Produces: `PlayerProfile.AvatarId` (`string`), `ProfileService.UpdateAvatarAsync(string avatarId)` (`Task<ProfileUpdateResult>`). Used by Task 7 (hydration) and Task 9 (`AvatarSelectionModal`).

- [ ] **Step 1: Write the failing test**

Add to `Assets/_Project/Tests/EditMode/Social/ProfileServiceTests.cs` (inside the existing `ProfileServiceTests` class):

```csharp
        [Test]
        public async Task GetProfileAsync_returns_avatarId_from_backend()
        {
            _backend.ProfileResponse = new PlayerProfile
            {
                PlayerId = "p1", DisplayName = "Stella", AvatarId = "avatar_wizard"
            };

            var profile = await _profiles.GetProfileAsync("p1");

            Assert.AreEqual("avatar_wizard", profile.AvatarId);
        }

        [Test]
        public async Task UpdateAvatarAsync_commits_avatarId_via_UpdateProfile()
        {
            _backend.ProfileUpdateResponse = new ProfileUpdateResult { Success = true };

            var result = await _profiles.UpdateAvatarAsync("avatar_wizard");

            Assert.AreEqual("UpdateProfile", _backend.CalledFunction);
            Assert.AreEqual("avatar_wizard", _backend.CalledArgs["avatarId"]);
            Assert.IsTrue(result.Success);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -testFilter ProfileServiceTests
```
Expected: FAIL — `PlayerProfile.AvatarId` and `ProfileService.UpdateAvatarAsync` don't exist yet (compile error).

- [ ] **Step 3: Add `AvatarId` to `PlayerProfile`**

In `Assets/_Project/Scripts/Social/PlayerProfile.cs`:

```csharp
    public class PlayerProfile
    {
        public string   PlayerId;
        public string   DisplayName;
        public string   AvatarId;
        public int      Level;
        public int      Xp;
        public string[] Badges;
        public int      TilesOwned;
        public bool     EmailVerified;
    }
```

- [ ] **Step 4: Add `UpdateAvatarAsync` to `ProfileService`**

In `Assets/_Project/Scripts/Social/ProfileService.cs`, add alongside `UpdateDisplayNameAsync`:

```csharp
        public Task<ProfileUpdateResult> UpdateAvatarAsync(string avatarId) =>
            _backend.CallAsync<ProfileUpdateResult>("UpdateProfile",
                new Dictionary<string, object> { { "avatarId", avatarId } });
```

- [ ] **Step 5: Run the tests to verify they pass**

Same command as Step 2. Expected: PASS (all `ProfileServiceTests`, including the two new ones).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Social/PlayerProfile.cs Assets/_Project/Scripts/Social/ProfileService.cs Assets/_Project/Tests/EditMode/Social/ProfileServiceTests.cs
git commit -m "social: add AvatarId to PlayerProfile and ProfileService.UpdateAvatarAsync"
```

---

### Task 4: Cloud Code — `avatarId` on `UpdateProfile`/`GetPlayerProfile`

**Files:**
- Modify: `ServerCode/UpdateProfile.js`
- Modify: `ServerCode/GetPlayerProfile.js`

**Interfaces:**
- Consumes: nothing new (same Cloud Save `player_profile` record).
- Produces: `UpdateProfile` now accepts optional `params.avatarId` (validated against a fixed 25-id list) alongside the existing optional-going-forward `params.displayName`; both endpoints round-trip `avatarId` in their JSON responses. Consumed by `ProfileService` (Task 3) via `IBackendClient`.

No automated test for these two files — consistent with this codebase's existing convention (Cloud Code modules are exercised manually via `CloudCodeTestHarness`, not unit tested).

- [ ] **Step 1: Rewrite `ServerCode/UpdateProfile.js`**

```javascript
// UpdateProfile — validates and commits the caller's display name and/or
// avatar into their "player_profile" Cloud Save record (merging with any
// existing profile fields such as level/xp/badges). Both params are
// independently optional: a call may update just the name, just the
// avatar, or both. The name is re-moderated server-side: the client's
// ChatModerationFilter check is only fast feedback.
// BLOCKED_WORDS / CHAR_MAP / MAX_DISPLAY_NAME_LENGTH must match
// SocialConfig.BlockedWords / ChatModerationFilter / MaxDisplayNameLength —
// same "must match" pattern as ModerateMessage.js (Cloud Code modules deploy
// as standalone files, so the filter is duplicated rather than required).
// AVATAR_IDS must match the 25 AvatarDefinition assets registered on
// DatabaseRegistry (see docs/superpowers/plans/2026-07-06-avatar-selection.md).
const { DataApi } = require("@unity-services/cloud-save-1.4");

const PROFILE_KEY = "player_profile";
const MAX_DISPLAY_NAME_LENGTH = 20; // must match SocialConfig.MaxDisplayNameLength

const BLOCKED_WORDS = [
  "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
  "nigger", "nigga", "whore", "slut", "retard", "kys"
];
const CHAR_MAP = { "@": "a", "4": "a", "1": "i", "!": "i", "0": "o", "3": "e", "$": "s", "5": "s", "7": "t" };

const AVATAR_IDS = [
  "avatar_alien_blue", "avatar_alien_green", "avatar_boy1", "avatar_boy_1_dark",
  "avatar_boy_2", "avatar_boy_3", "avatar_boy_4", "avatar_boy_5", "avatar_boy_6",
  "avatar_boy_6_light", "avatar_boy_7", "avatar_boy_8", "avatar_boy_9", "avatar_boy_10",
  "avatar_dark", "avatar_girl_1", "avatar_girl_2", "avatar_girl_2_dark", "avatar_girl_3",
  "avatar_girl_4", "avatar_girl_5", "avatar_girl_6", "avatar_girl_7", "avatar_girl_8",
  "avatar_wizard"
];

function isClean(text) {
  let normalized = "";
  for (const ch of text.toLowerCase()) normalized += CHAR_MAP[ch] ?? ch;
  return !BLOCKED_WORDS.some(word => normalized.includes(word));
}

/**
 * @param {string} [params.displayName] - New display name, 1-20 chars, must pass moderation.
 * @param {string} [params.avatarId] - New avatar id, must be one of AVATAR_IDS.
 */
module.exports = async ({ params, context, logger }) => {
  const hasDisplayName = params.displayName !== undefined && params.displayName !== null;
  const hasAvatarId    = params.avatarId    !== undefined && params.avatarId    !== null;

  let displayName = null;
  if (hasDisplayName) {
    displayName = params.displayName.trim();

    if (displayName.length === 0) {
      return { success: false, reason: "NAME_EMPTY", displayName: null, avatarId: null };
    }
    if (displayName.length > MAX_DISPLAY_NAME_LENGTH) {
      return { success: false, reason: "NAME_TOO_LONG", displayName: null, avatarId: null };
    }
    if (!isClean(displayName)) {
      logger.info(`UpdateProfile: rejected display name from ${context.playerId}`);
      return { success: false, reason: "NAME_REJECTED", displayName: null, avatarId: null };
    }
  }

  let avatarId = null;
  if (hasAvatarId) {
    avatarId = params.avatarId;
    if (!AVATAR_IDS.includes(avatarId)) {
      logger.info(`UpdateProfile: rejected unknown avatarId "${avatarId}" from ${context.playerId}`);
      return { success: false, reason: "AVATAR_INVALID", displayName: null, avatarId: null };
    }
  }

  const { projectId, playerId, accessToken } = context;
  const saveApi = new DataApi({ headers: { Authorization: `Bearer ${accessToken}` } });

  let profile = {};
  try {
    const res  = await saveApi.getItems({ projectId, playerId, key: [PROFILE_KEY] });
    const item = res.data.results.find(r => r.key === PROFILE_KEY);
    if (item?.value) profile = typeof item.value === "string" ? JSON.parse(item.value) : item.value;
  } catch (_) { /* no profile yet */ }

  if (hasDisplayName) profile.displayName = displayName;
  if (hasAvatarId)    profile.avatarId    = avatarId;
  profile.updatedMs = Date.now();

  await saveApi.setItem({ projectId, playerId, key: PROFILE_KEY, body: { value: profile } });

  logger.info(`UpdateProfile: ${playerId} → displayName=${profile.displayName ?? "(unchanged)"} avatarId=${profile.avatarId ?? "(unchanged)"}`);
  return {
    success: true,
    reason: null,
    displayName: profile.displayName ?? null,
    avatarId: profile.avatarId ?? null
  };
};
```

- [ ] **Step 2: Update `ServerCode/GetPlayerProfile.js`**

Change the return statement (the rest of the file — the `DataApi(context)`/positional-args fix, the display-name-defaults-to-null comment — stays as-is):

```javascript
  return {
    playerId:     targetId,
    displayName:  profile?.displayName ?? null,
    avatarId:     profile?.avatarId ?? null,
    level:        profile?.level ?? 1,
    xp:           profile?.xp ?? 0,
    badges:       profile?.badges ?? [],
    tilesOwned,
    emailVerified: profile?.emailVerified ?? false
  };
```

- [ ] **Step 3: Commit**

```bash
git add ServerCode/UpdateProfile.js ServerCode/GetPlayerProfile.js
git commit -m "servercode: add avatarId to UpdateProfile/GetPlayerProfile"
```

---

### Task 5: `PlayerState.AvatarId`

**Files:**
- Modify: `Assets/_Project/Scripts/Progression/PlayerState.cs`
- Test: `Assets/_Project/Tests/EditMode/Progression/PlayerStateAvatarTests.cs` (new)

**Interfaces:**
- Consumes: nothing new.
- Produces: `PlayerState.AvatarId` (`string`), `PlayerState.SetAvatarId(string)`, `PlayerState.OnAvatarChanged` (`Action<string>`). Used by Task 7 (hydration), Task 9 (`AvatarSelectionModal`), Task 10 (`HUDController`).

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using SocialUniverse.Progression;

namespace SocialUniverse.Tests
{
    public class PlayerStateAvatarTests
    {
        [Test]
        public void SetAvatarId_sets_field_and_fires_event()
        {
            var playerState = new PlayerState();
            string eventAvatarId = null;
            playerState.OnAvatarChanged += id => eventAvatarId = id;

            playerState.SetAvatarId("avatar_wizard");

            Assert.AreEqual("avatar_wizard", playerState.AvatarId);
            Assert.AreEqual("avatar_wizard", eventAvatarId);
        }

        [Test]
        public void AvatarId_defaults_to_null()
        {
            var playerState = new PlayerState();

            Assert.IsNull(playerState.AvatarId);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -testFilter PlayerStateAvatarTests
```
Expected: FAIL — `PlayerState.AvatarId`/`SetAvatarId`/`OnAvatarChanged` don't exist yet (compile error).

- [ ] **Step 3: Extend `PlayerState`**

In `Assets/_Project/Scripts/Progression/PlayerState.cs`, add alongside `DisplayName`/`SetDisplayName`/`OnDisplayNameChanged`:

```csharp
        public string DisplayName { get; private set; } = "Player";
        public string AvatarId    { get; private set; }
```
```csharp
        public event Action<string> OnDisplayNameChanged;
        public event Action<string> OnAvatarChanged;
```
```csharp
        public void SetAvatarId(string avatarId)
        {
            AvatarId = avatarId;
            OnAvatarChanged?.Invoke(avatarId);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2. Expected: PASS (2/2).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Progression/PlayerState.cs Assets/_Project/Tests/EditMode/Progression/PlayerStateAvatarTests.cs
git commit -m "progression: add AvatarId to PlayerState"
```

---

### Task 6: `AvatarAssignment.ResolveAvatarId` (pure random-pick helper)

**Files:**
- Create: `Assets/_Project/Scripts/Progression/AvatarAssignment.cs`
- Test: `Assets/_Project/Tests/EditMode/Progression/AvatarAssignmentTests.cs` (new)

**Interfaces:**
- Consumes: nothing new (deliberately dependency-free — a `string`, an `IReadOnlyList<string>`, and a `Func<int, int>`).
- Produces: `AvatarAssignment.ResolveAvatarId(string existingAvatarId, IReadOnlyList<string> catalogAvatarIds, Func<int, int> randomIndex)` → `string`. Used by Task 7 (`PlanetSceneScope.HydrateServerStateAsync`).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using SocialUniverse.Progression;

namespace SocialUniverse.Tests
{
    public class AvatarAssignmentTests
    {
        [Test]
        public void Existing_avatar_id_is_returned_unchanged()
        {
            var catalog = new List<string> { "avatar_a", "avatar_b" };

            string resolved = AvatarAssignment.ResolveAvatarId("avatar_b", catalog, n => 0);

            Assert.AreEqual("avatar_b", resolved);
        }

        [Test]
        public void Empty_avatar_id_picks_from_catalog_using_the_random_index()
        {
            var catalog = new List<string> { "avatar_a", "avatar_b", "avatar_c" };

            string resolved = AvatarAssignment.ResolveAvatarId("", catalog, n => 2);

            Assert.AreEqual("avatar_c", resolved);
        }

        [Test]
        public void Null_avatar_id_picks_from_catalog()
        {
            var catalog = new List<string> { "avatar_a" };

            string resolved = AvatarAssignment.ResolveAvatarId(null, catalog, n => 0);

            Assert.AreEqual("avatar_a", resolved);
        }

        [Test]
        public void Empty_catalog_returns_null()
        {
            string resolved = AvatarAssignment.ResolveAvatarId(null, new List<string>(), n => 0);

            Assert.IsNull(resolved);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode -testFilter AvatarAssignmentTests
```
Expected: FAIL — `AvatarAssignment` doesn't exist yet (compile error).

- [ ] **Step 3: Write the helper**

```csharp
using System;
using System.Collections.Generic;

namespace SocialUniverse.Progression
{
    // Pure resolution logic for PlanetSceneScope.HydrateServerStateAsync's
    // avatar-assignment fallback — kept free of DatabaseRegistry/ProfileService
    // so it's unit-testable without Unity or network dependencies.
    public static class AvatarAssignment
    {
        public static string ResolveAvatarId(string existingAvatarId, IReadOnlyList<string> catalogAvatarIds, Func<int, int> randomIndex)
        {
            if (!string.IsNullOrEmpty(existingAvatarId))
                return existingAvatarId;

            if (catalogAvatarIds == null || catalogAvatarIds.Count == 0)
                return null;

            int index = randomIndex(catalogAvatarIds.Count);
            return catalogAvatarIds[index];
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as Step 2. Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Progression/AvatarAssignment.cs Assets/_Project/Tests/EditMode/Progression/AvatarAssignmentTests.cs
git commit -m "progression: add AvatarAssignment.ResolveAvatarId helper"
```

---

### Task 7: Wire random assignment into `PlanetSceneScope.HydrateServerStateAsync`

**Files:**
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs:265-290`

**Interfaces:**
- Consumes: `AvatarAssignment.ResolveAvatarId` (Task 6), `DatabaseRegistry.AllAvatars` (Task 2), `PlayerState.SetAvatarId` (Task 5), `ProfileService.UpdateAvatarAsync` (Task 3), `PlayerProfile.AvatarId` (Task 3).
- Produces: on every Planet-scene load, `PlayerState.AvatarId` ends up either the profile's existing value or a freshly-persisted random pick.

No new automated test for this step — `HydrateServerStateAsync` is a private method on `PlanetSceneBootstrapper`, a plain class with a dozen heavy MonoBehaviour/service constructor dependencies that isn't unit-tested anywhere in this codebase today (it's only exercised, indirectly, by loading the real scene in `PlanetSceneFlowTests.cs`, and that suite's `BackendClient` calls fail against CI's no-credentials environment the same way they already do for display name/email-verified hydration — see the design doc's Testing section). The decision logic itself is already covered by `AvatarAssignmentTests` (Task 6); this task is manually verified in Play Mode as part of Task 11's checklist.

- [ ] **Step 1: Edit `HydrateServerStateAsync`**

In `Assets/_Project/Scripts/App/PlanetSceneScope.cs`, inside the `try` block that currently reads:

```csharp
            try
            {
                var profile = await _profileService.GetProfileAsync(_auth.PlayerId);
                if (profile != null)
                {
                    if (!string.IsNullOrEmpty(profile.DisplayName))
                        _playerState.SetDisplayName(profile.DisplayName);

                    _playerState.SetEmailVerified(profile.EmailVerified);

                    if (!profile.EmailVerified)
                    {
                        string promptedKey = SaveKeys.EmailVerificationPromptedKey(_auth.PlayerId);
                        if (!PlayerPrefs.HasKey(promptedKey))
                        {
                            PlayerPrefs.SetInt(promptedKey, 1);
                            PlayerPrefs.Save();
                            EventBus.Publish(new ShowEmailVerificationPromptEvent());
                        }
                    }
                }
            }
```

insert the avatar-resolution block right after `_playerState.SetEmailVerified(profile.EmailVerified);`:

```csharp
            try
            {
                var profile = await _profileService.GetProfileAsync(_auth.PlayerId);
                if (profile != null)
                {
                    if (!string.IsNullOrEmpty(profile.DisplayName))
                        _playerState.SetDisplayName(profile.DisplayName);

                    _playerState.SetEmailVerified(profile.EmailVerified);

                    var catalogIds = _registry.AllAvatars.Select(a => a.AvatarId).ToList();
                    string resolvedAvatarId = AvatarAssignment.ResolveAvatarId(profile.AvatarId, catalogIds, n => UnityEngine.Random.Range(0, n));
                    _playerState.SetAvatarId(resolvedAvatarId);

                    if (string.IsNullOrEmpty(profile.AvatarId) && !string.IsNullOrEmpty(resolvedAvatarId))
                    {
                        try
                        {
                            await _profileService.UpdateAvatarAsync(resolvedAvatarId);
                        }
                        catch (Exception ex)
                        {
                            SULog.Warn($"PlanetSceneBootstrapper: avatar assignment failed to persist ({ex.Message}), using local pick", SULog.Channel.Net);
                        }
                    }

                    if (!profile.EmailVerified)
                    {
                        string promptedKey = SaveKeys.EmailVerificationPromptedKey(_auth.PlayerId);
                        if (!PlayerPrefs.HasKey(promptedKey))
                        {
                            PlayerPrefs.SetInt(promptedKey, 1);
                            PlayerPrefs.Save();
                            EventBus.Publish(new ShowEmailVerificationPromptEvent());
                        }
                    }
                }
            }
```

`System.Linq` and `SocialUniverse.Progression` are both already imported at the top of this file (lines 3 and 13), so no new `using` statements are needed.

- [ ] **Step 2: Let Unity compile and confirm no errors**

Use `mcp__UnityMCP__read_console` (or the Editor Console) after the domain reload; expect zero errors/warnings referencing `PlanetSceneScope.cs`.

- [ ] **Step 3: Run the full EditMode suite to confirm no regressions**

```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -runTests -projectPath . -testResults results.xml -testPlatform EditMode
```
Expected: PASS (all EditMode tests, including every test added in Tasks 2/3/5/6).

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "app: assign a random avatar on first hydration when the profile has none"
```

---

### Task 8: Create the 25 `AvatarDefinition` assets and register them on `DatabaseRegistry`

**Files:**
- Create: 25 assets under `Assets/_Project/ScriptableObjects/Avatars/` (see Global Constraints table for names/ids).
- Modify: `Assets/_Project/ScriptableObjects/DatabaseRegistry.asset` (via Unity, not a text edit).

**Interfaces:**
- Consumes: `AvatarDefinition` (Task 1), `DatabaseRegistry._avatars` (Task 2), the 25 sprite files at `Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/`.
- Produces: `DatabaseRegistry.asset`'s `AllAvatars` returns all 25 entries at runtime — this is what Task 7/9/10 read from.

This is a Unity Editor asset-authoring task, done through `mcp__UnityMCP__manage_scriptable_object` and `mcp__UnityMCP__batch_execute` rather than hand-written YAML (avoids manually transcribing sprite GUIDs). The tool's default batch cap is 25 commands, so creation and patching are split into two batches.

- [ ] **Step 1: Create the folder**

```
mcp__UnityMCP__manage_asset(action="create_folder", path="Assets/_Project/ScriptableObjects/Avatars")
```

- [ ] **Step 2: Batch-create all 25 empty `AvatarDefinition` assets**

```
mcp__UnityMCP__batch_execute(commands=[
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_AlienBlue",   "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_AlienGreen",  "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy1",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy1Dark",    "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy2",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy3",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy4",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy5",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy6",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy6Light",   "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy7",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy8",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy9",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Boy10",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Dark",        "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl1",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl2",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl2Dark",   "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl3",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl4",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl5",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl6",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl7",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Girl8",       "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}},
  {"tool": "manage_scriptable_object", "params": {"action": "create", "type_name": "SocialUniverse.Config.AvatarDefinition", "asset_name": "Avatar_Wizard",      "folder_path": "Assets/_Project/ScriptableObjects/Avatars"}}
])
```

Verify the response reports 25 successes (0 failures) before continuing.

- [ ] **Step 3: Batch-patch `_avatarId` and `_sprite` on all 25 assets**

```
mcp__UnityMCP__batch_execute(commands=[
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_AlienBlue.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_alien_blue"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Alien Blue.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_AlienGreen.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_alien_green"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Alien Green.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy1.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy1"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy1.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy1Dark.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_1_dark"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 1 Dark.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy2.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_2"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 2.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy3.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_3"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 3.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy4.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_4"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 4.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy5.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_5"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 5.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy6.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_6"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 6.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy6Light.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_6_light"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 6 Light.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy7.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_7"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 7.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy8.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_8"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 8.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy9.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_9"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 9.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy10.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_boy_10"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Boy 10.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Dark.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_dark"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Dark.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl1.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_1"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 1.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl2.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_2"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 2.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl2Dark.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_2_dark"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 2 Dark.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl3.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_3"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 3.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl4.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_4"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 4.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl5.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_5"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 5.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl6.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_6"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 6.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl7.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_7"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 7.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl8.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_girl_8"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Girl 8.png"}}}]}},
  {"tool": "manage_scriptable_object", "params": {"action": "modify", "target": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Wizard.asset"}, "patches": [{"path": "_avatarId", "value": "avatar_wizard"}, {"path": "_sprite", "value": {"ref": {"path": "Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Wizard.png"}}}]}}
])
```

Verify the response reports 25 successes (0 failures) before continuing. If any sprite import isn't a single-sprite texture (multi-sprite sheet), that entry's `_sprite` patch needs a `spriteName` alongside `path`/`guid` — check `mcp__UnityMCP__manage_asset(action="get_info", path="Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/<file>.png")` for that file's `spriteMode` first if a patch fails.

- [ ] **Step 4: Register all 25 on `DatabaseRegistry.asset`**

```
mcp__UnityMCP__manage_scriptable_object(action="modify", target={"path": "Assets/_Project/ScriptableObjects/DatabaseRegistry.asset"}, patches=[
  {"path": "_avatars", "value": [
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_AlienBlue.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_AlienGreen.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy1.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy1Dark.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy2.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy3.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy4.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy5.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy6.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy6Light.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy7.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy8.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy9.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Boy10.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Dark.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl1.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl2.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl2Dark.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl3.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl4.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl5.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl6.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl7.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Girl8.asset"}},
    {"ref": {"path": "Assets/_Project/ScriptableObjects/Avatars/Avatar_Wizard.asset"}}
  ]}
])
```

- [ ] **Step 5: Verify via `execute_code`**

```
mcp__UnityMCP__execute_code(code: load the DatabaseRegistry asset at "Assets/_Project/ScriptableObjects/DatabaseRegistry.asset" via AssetDatabase.LoadAssetAtPath<DatabaseRegistry>, then log registry.AllAvatars.Count() and confirm it prints 25, and that registry.GetAvatar("avatar_wizard") is non-null with a non-null Sprite)
```

- [ ] **Step 6: Commit**

The 25 `.asset` files, their `.meta` files, and `DatabaseRegistry.asset` are all binary/YAML Unity assets — stage and commit them as-is (no hand-authored diff to review beyond confirming the file count).

```bash
git add Assets/_Project/ScriptableObjects/Avatars/ Assets/_Project/ScriptableObjects/DatabaseRegistry.asset
git commit -m "config: create the 25-avatar catalog and register it on DatabaseRegistry"
```

---

### Task 9: `AvatarSelectionModal`

**Files:**
- Create: `Assets/_Project/Scripts/UI/AvatarSelectionModal.cs`

**Interfaces:**
- Consumes: `PlayerState.AvatarId`/`SetAvatarId` (Task 5), `ProfileService.UpdateAvatarAsync` (Task 3), `DatabaseRegistry.AllAvatars` (Task 2), `AvatarDefinition.AvatarId`/`Sprite` (Task 1).
- Produces: `AvatarSelectionModal.Open()`/`Close()` (public, no-arg). Used by Task 10 (`HUDController`) and wired into the scene in Task 11.

No automated test for this file — consistent with `DisplayNameModal` (the codebase's existing convention for MonoBehaviour UI: manually verified in Play Mode, not unit tested).

- [ ] **Step 1: Write the class**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using SocialUniverse.Core;
using SocialUniverse.Social;
using SocialUniverse.Progression;
using SocialUniverse.Config;

namespace SocialUniverse.UI
{
    // Grid picker for the player's profile avatar. Mirrors DisplayNameModal's
    // injected-services / Open() / Close() shape. Unlike DisplayNameModal
    // there's no separate confirm step — tapping an avatar commits
    // immediately, the same way tapping a hex tile does.
    public class AvatarSelectionModal : MonoBehaviour
    {
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private Button    _avatarButtonPrefab;  // Button + Image, one avatar tile; starts inactive
        [SerializeField] private Button    _cancelButton;
        [SerializeField] private TMP_Text  _statusText;

        [Inject] private PlayerState      _playerState;
        [Inject] private ProfileService   _profiles;
        [Inject] private DatabaseRegistry _registry;

        private readonly List<(Button Button, string AvatarId)> _entries = new();
        private bool _built;

        private void Awake()
        {
            _cancelButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            if (!_built) BuildGrid();
            RefreshHighlight();
            _statusText.text = "";
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);

        private void BuildGrid()
        {
            foreach (var avatar in _registry.AllAvatars)
            {
                string avatarId = avatar.AvatarId;
                var button = Instantiate(_avatarButtonPrefab, _gridContainer);
                button.gameObject.SetActive(true);

                var image = button.GetComponent<Image>();
                if (image != null) image.sprite = avatar.Sprite;

                button.onClick.AddListener(() => OnAvatarClicked(avatarId));
                _entries.Add((button, avatarId));
            }
            _built = true;
        }

        private async void OnAvatarClicked(string avatarId)
        {
            if (avatarId == _playerState.AvatarId) return;

            SetBusy(true);
            _statusText.text = "Saving…";

            try
            {
                var result = await _profiles.UpdateAvatarAsync(avatarId);

                // null result means mock backend — treat as success with local id
                if (result == null || result.Success)
                {
                    _playerState.SetAvatarId(avatarId);
                    Close();
                }
                else
                {
                    _statusText.text = result.Reason == "AVATAR_INVALID"
                        ? "That avatar isn't available"
                        : "Could not update — please try again";
                }
            }
            catch (Exception ex)
            {
                _statusText.text = "Error updating avatar";
                SULog.Warn($"AvatarSelectionModal: update failed ({ex.Message})", SULog.Channel.Net);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RefreshHighlight()
        {
            foreach (var entry in _entries)
                entry.Button.interactable = entry.AvatarId != _playerState.AvatarId;
        }

        private void SetBusy(bool busy)
        {
            _cancelButton.interactable = !busy;
            if (busy)
            {
                foreach (var entry in _entries) entry.Button.interactable = false;
            }
            else
            {
                RefreshHighlight();
            }
        }
    }
}
```

- [ ] **Step 2: Let Unity compile and confirm no errors**

Use `mcp__UnityMCP__read_console`; expect zero errors/warnings referencing `AvatarSelectionModal.cs`.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/UI/AvatarSelectionModal.cs
git commit -m "ui: add AvatarSelectionModal grid picker"
```

---

### Task 10: `HUDController` avatar icon

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HUDController.cs`

**Interfaces:**
- Consumes: `PlayerState.AvatarId`/`OnAvatarChanged` (Task 5), `DatabaseRegistry.GetAvatar` (Task 2), `AvatarSelectionModal.Open()` (Task 9).
- Produces: HUD renders the current avatar sprite and opens the picker on click. Wired into the scene in Task 11.

No automated test for this file — same MonoBehaviour-UI convention as Task 9.

- [ ] **Step 1: Add serialized fields and an injected registry**

```csharp
        [SerializeField] private CurrencyView _currency;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private Slider _fuelSlider;
        [SerializeField] private Text _miningStatusText;
        [SerializeField] private Text _landStatusText;
        [SerializeField] private TMP_Text _usernameText;
        [SerializeField] private Button _usernameButton;
        [SerializeField] private DisplayNameModal _displayNameModal;
        [SerializeField] private Image _avatarImage;
        [SerializeField] private Button _avatarButton;
        [SerializeField] private AvatarSelectionModal _avatarSelectionModal;
        [SerializeField] private EmailVerificationModal _emailVerificationModal;
        [SerializeField] private Button _verifyEmailButton;
        [SerializeField] private TMP_Text _asteroidRefreshText;
        [SerializeField] private Button _chatButton;
        [SerializeField] private SocialDebugPanel _socialPanel;
        [SerializeField] private Toggle _tileViewToggle;
        [SerializeField] private TMP_Text _explorersText;
        [SerializeField] private Button _launchButton;
        [SerializeField] private TMP_Text _planetNameText;

        [Inject] private Wallet _wallet;
        [Inject] private PlayerState _playerState;
        [Inject] private MiningController _mining;
        [Inject] private HexasphereManager _hexasphere;
        [Inject] private AsteroidSpawner _asteroidSpawner;
        [Inject] private IPresenceService _presence;
        [Inject] private PlanetDefinition _planet;
        [Inject] private DatabaseRegistry _registry;
```

- [ ] **Step 2: Wire it up in `Start()`/`OnDestroy()`**

```csharp
        private void Start()
        {
            _currency.Bind(_wallet);
            _chatButton.onClick.AddListener(_socialPanel.Open);
            _usernameButton?.onClick.AddListener(OnUsernameClicked);
            _avatarButton?.onClick.AddListener(() => _avatarSelectionModal?.Open());
            _launchButton?.onClick.AddListener(() => EventBus.Publish(new LaunchRequestedEvent()));
            if (_verifyEmailButton != null) _verifyEmailButton.onClick.AddListener(() => _emailVerificationModal?.Open());
            EventBus.Subscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);

            // Tiles hidden by default; toggled by the view-land-tile toggle.
            _hexasphere.SetTilesVisible(false);
            if (_tileViewToggle != null)
            {
                _tileViewToggle.SetIsOnWithoutNotify(false);
                _tileViewToggle.onValueChanged.AddListener(_hexasphere.SetTilesVisible);
            }

            _playerState.OnLevelChanged       += SetLevel;
            _playerState.OnFuelChanged        += SetFuel;
            _playerState.OnDisplayNameChanged += SetUsername;
            _playerState.OnAvatarChanged      += SetAvatar;
            _presence.PresenceChanged         += RefreshExplorerCount;

            if (_planetNameText != null) _planetNameText.text = _planet.DisplayName;

            SetLevel(_playerState.Level);
            SetFuel(_playerState.Fuel);
            SetUsername(_playerState.DisplayName);
            SetAvatar(_playerState.AvatarId);
            RefreshMiningStatus();
            RefreshLandStatus();
            RefreshAsteroidRefresh();
            RefreshExplorerCount();
        }

        private void OnDestroy()
        {
            _playerState.OnLevelChanged       -= SetLevel;
            _playerState.OnFuelChanged        -= SetFuel;
            _playerState.OnDisplayNameChanged -= SetUsername;
            _playerState.OnAvatarChanged      -= SetAvatar;
            _presence.PresenceChanged         -= RefreshExplorerCount;
            EventBus.Unsubscribe<ShowEmailVerificationPromptEvent>(OnShowEmailVerificationPrompt);
        }
```

- [ ] **Step 3: Add `SetAvatar`**

```csharp
        private void SetAvatar(string avatarId)
        {
            if (_avatarImage == null) return;
            var avatar = _registry.GetAvatar(avatarId);
            if (avatar != null) _avatarImage.sprite = avatar.Sprite;
        }
```

- [ ] **Step 4: Let Unity compile and confirm no errors**

Use `mcp__UnityMCP__read_console`; expect zero errors/warnings referencing `HUDController.cs`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/HUDController.cs
git commit -m "ui: show the player's avatar in the HUD and open the picker on click"
```

---

### Task 11: Scene wiring — HUD avatar icon, `AvatarSelectionModal` GameObject, DI registration, manual verification

**Files:**
- Modify: `Assets/Scenes/Planet.unity` (via Unity Editor, not a text edit)
- Modify: `Assets/_Project/Scripts/App/PlanetSceneScope.cs:124` (one-line DI registration)

**Interfaces:**
- Consumes: `AvatarSelectionModal` (Task 9), `HUDController`'s new fields (Task 10).
- Produces: a working, in-scene avatar picker reachable from the HUD.

- [ ] **Step 1: Register `AvatarSelectionModal` in the DI container**

In `Assets/_Project/Scripts/App/PlanetSceneScope.cs`, next to the existing `DisplayNameModal` registration:

```csharp
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.DisplayNameModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.AvatarSelectionModal>();
            builder.RegisterComponentInHierarchy<SocialUniverse.UI.EmailVerificationModal>();
```

- [ ] **Step 2: Inspect the existing HUD hierarchy**

```
mcp__UnityMCP__manage_scene(action="get_hierarchy", path="Assets/Scenes/Planet.unity")
```
Find the `HUDController` GameObject and note the existing username button's structure (its `Button`+`Image`+child `TMP_Text` hierarchy) — the new avatar button mirrors it minus the text.

- [ ] **Step 3: Duplicate the username button to make the avatar button**

```
mcp__UnityMCP__manage_gameobject(action="duplicate", target="<UsernameButton instance ID from Step 2>", new_name="AvatarButton")
```
Then, on the duplicate: remove its child `TMP_Text` (it's a portrait icon, not a text button), keep its `Image` component (this becomes `_avatarImage`), position it directly to the left of the username button (use `manage_gameobject(action="modify", ...)` to adjust its `RectTransform` anchored position so the two sit side by side — match the username button's height for a square icon).

- [ ] **Step 4: Wire the new HUD fields**

```
mcp__UnityMCP__manage_components(action="set_property", target="HUDController", component_type="HUDController", properties={
  "_avatarImage": {"path": "HUD/.../AvatarButton"},
  "_avatarButton": {"path": "HUD/.../AvatarButton"}
})
```
(Adjust the `path` values to the actual hierarchy path found in Step 2/3.)

- [ ] **Step 5: Build the `AvatarSelectionModal` GameObject**

Create a new inactive panel under the same Canvas as `DisplayNameModal` (inspect `DisplayNameModal`'s GameObject via `find_gameobjects(search_method="by_component", search_term="DisplayNameModal")` first, and build the new modal as a sibling):
- A background panel `Image`.
- A `ScrollRect` with a `Content` child using a `GridLayoutGroup` (cell size ~96x96, spacing ~8) — this is `_gridContainer`.
- One template avatar button (a `Button` + `Image`, ~96x96, initially inactive) saved as `_avatarButtonPrefab` — since the modal instantiates 25 of these at `Open()` time, this template can live as an inactive child of the modal itself rather than a separate prefab asset (matches `Instantiate(_avatarButtonPrefab, _gridContainer)` in Task 9's code, which works equally well with a scene-object template).
- A `Cancel` `Button` + `TMP_Text` status label, mirroring `DisplayNameModal`'s `_cancelButton`/`_statusText`.
- Add the `AvatarSelectionModal` component to the panel's root GameObject and wire `_gridContainer`, `_avatarButtonPrefab`, `_cancelButton`, `_statusText` via `manage_components(action="set_property", ...)`.

- [ ] **Step 6: Save the scene**

```
mcp__UnityMCP__manage_scene(action="save")
```

- [ ] **Step 7: Manual Play Mode verification checklist**

Enter Play Mode on `Assets/Scenes/Planet.unity` (standalone, no Bootstrap needed — `PlanetSceneScope` runs in standalone mode) and confirm:
1. A random avatar appears in the HUD without any user action (first-time profile has no `avatarId`, so `HydrateServerStateAsync`'s fallback assigns one — note: this requires a backend that can actually complete `GetPlayerProfile`/`UpdateProfile`, i.e. a signed-in session against the real UGS project or a `CloudCodeTestHarness` stand-in; against a bare `BackendClient` with no UGS session, the fetch throws, is caught, and `AvatarId` stays `null` — the HUD icon should just stay blank in that case, which is the same "no server" degrade-gracefully behavior `DisplayName`/`EmailVerified` already have).
2. Clicking the new HUD avatar icon opens `AvatarSelectionModal` showing all 25 avatars, with the current one shown as non-interactable (highlighted).
3. Clicking a different avatar updates the HUD icon and closes the modal.
4. Reloading the scene shows the same avatar (persisted, not re-randomized).

- [ ] **Step 8: Commit**

```bash
git add Assets/Scenes/Planet.unity Assets/_Project/Scripts/App/PlanetSceneScope.cs
git commit -m "app: wire the avatar picker into the Planet scene HUD"
```
