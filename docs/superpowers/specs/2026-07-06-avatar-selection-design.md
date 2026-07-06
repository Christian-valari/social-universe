# Avatar Selection — Design

## Context

Players currently have no visual identity beyond their display name. `Social_Universe_Architecture.md` lists "player avatar" as a missing 3D model (for in-world representation), but this design is about a separate, smaller thing: a 2D profile-picture avatar, picked from the pre-existing sprite set at `Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/`. It follows the same shape as the existing display-name feature (`ProfileService`/`DisplayNameModal`/`PlayerState.DisplayName`): a player-chosen, server-persisted profile field, editable from the Planet-scene HUD.

Of the 27 sprites in that folder, two are not real character portraits — `Empty.png` (fully blank/transparent) and `Empty Gray.png` (a generic placeholder silhouette, not a distinct character) — and are excluded from the pickable set. The remaining 25 are real, distinct character portraits (including light/dark skin-tone variants, which are legitimate separate choices, not duplicates).

**Scope for this design:** self-only. The avatar shows in the local player's own HUD and picker. `IPresenceService`/`PresencePlayer` currently carries only `PlayerId`/`DisplayName` — showing other nearby players' avatars would require extending the presence roster and is deliberately left as a follow-up.

## Goal

New signups get a random avatar automatically. Any player can change it later from the Planet-scene HUD. The choice is server-persisted (survives reinstall/relogin) via the same Cloud Code profile record display name already uses.

## Components

### 1. `AvatarDefinition` (new, `Config/`)

One ScriptableObject asset per avatar, mirroring `DroneDefinition`:

```csharp
[CreateAssetMenu(menuName = "SocialUniverse/Config/AvatarDefinition", fileName = "NewAvatar")]
public class AvatarDefinition : ScriptableObject
{
    [SerializeField] private string _avatarId;
    [SerializeField] private Sprite _sprite;

    public string AvatarId => _avatarId;
    public Sprite Sprite   => _sprite;
}
```

25 assets under `Assets/_Project/ScriptableObjects/Avatars/`, named `Avatar_<Name>.asset`, `_avatarId` snake_cased with an `avatar_` prefix (matching the `drone_scout`/`asteroid` id convention):

All 25, one per surviving sprite (source filename → id): Alien Blue → `avatar_alien_blue`, Alien Green → `avatar_alien_green`, Boy1 → `avatar_boy1`, Boy 1 Dark → `avatar_boy_1_dark`, Boy 2 → `avatar_boy_2`, Boy 3 → `avatar_boy_3`, Boy 4 → `avatar_boy_4`, Boy 5 → `avatar_boy_5`, Boy 6 → `avatar_boy_6`, Boy 6 Light → `avatar_boy_6_light`, Boy 7 → `avatar_boy_7`, Boy 8 → `avatar_boy_8`, Boy 9 → `avatar_boy_9`, Boy 10 → `avatar_boy_10`, Dark → `avatar_dark`, Girl 1 → `avatar_girl_1`, Girl 2 → `avatar_girl_2`, Girl 2 Dark → `avatar_girl_2_dark`, Girl 3 → `avatar_girl_3`, Girl 4 → `avatar_girl_4`, Girl 5 → `avatar_girl_5`, Girl 6 → `avatar_girl_6`, Girl 7 → `avatar_girl_7`, Girl 8 → `avatar_girl_8`, Wizard → `avatar_wizard`.

### 2. `DatabaseRegistry` (extended)

```csharp
[SerializeField] private AvatarDefinition[] _avatars;
public IEnumerable<AvatarDefinition> AllAvatars => _avatars ?? Array.Empty<AvatarDefinition>();
public AvatarDefinition GetAvatar(string avatarId) => Array.Find(_avatars, a => a.AvatarId == avatarId);
```

Same shape as `AllDrones`/`GetDrone`. Already injected wherever `DatabaseRegistry` is (including `PlanetSceneScope`), so no new DI wiring is needed to reach it.

### 3. `PlayerProfile` (client DTO) + Cloud Code

Add `public string AvatarId;` to `PlayerProfile.cs`.

**`GetPlayerProfile.js`**: return `avatarId: profile?.avatarId ?? null` alongside existing fields.

**`UpdateProfile.js`**: accept an optional `params.avatarId` alongside the existing `displayName` param (both become independently optional — a call may update just the name, just the avatar, or both, merging into the same Cloud Save record as today). Validate against a hardcoded list of the 25 known ids (duplicated from `DatabaseRegistry`, same "must match" comment convention as `BLOCKED_WORDS`/`MAX_DISPLAY_NAME_LENGTH`); unknown id → `{ success: false, reason: "AVATAR_INVALID" }`.

### 4. `ProfileService` (extended)

```csharp
public Task<ProfileUpdateResult> UpdateAvatarAsync(string avatarId) =>
    _backend.CallAsync<ProfileUpdateResult>("UpdateProfile",
        new Dictionary<string, object> { { "avatarId", avatarId } });
```

Same `ProfileUpdateResult` DTO as `UpdateDisplayNameAsync` (its `DisplayName` field is simply irrelevant/unset on an avatar-only call). No client-side validation beyond "is this one of `_registry.AllAvatars`" — the modal only ever offers catalog ids, so there's nothing to check client-side the way display-name length/moderation needs checking.

### 5. `PlayerState` (extended)

```csharp
public string AvatarId { get; private set; }
public event Action<string> OnAvatarChanged;

public void SetAvatarId(string avatarId)
{
    AvatarId = avatarId;
    OnAvatarChanged?.Invoke(avatarId);
}
```

Mirrors the existing `DisplayName`/`SetDisplayName`/`OnDisplayNameChanged` triplet exactly.

### 6. `PlanetSceneScope.HydrateServerStateAsync` (extended)

Immediately after the existing profile fetch (same try/catch block that already hydrates `DisplayName`/`EmailVerified`):

```csharp
if (!string.IsNullOrEmpty(profile.AvatarId))
{
    _playerState.SetAvatarId(profile.AvatarId);
}
else
{
    var pick = _registry.AllAvatars.ElementAtOrDefault(UnityEngine.Random.Range(0, _registry.AllAvatars.Count()));
    if (pick != null)
    {
        _playerState.SetAvatarId(pick.AvatarId);
        try { await _profiles.UpdateAvatarAsync(pick.AvatarId); }
        catch (Exception ex) { SULog.Warn($"PlanetSceneBootstrapper: avatar assignment failed to persist ({ex.Message}), using local pick", SULog.Channel.Net); }
    }
}
```

A failed persist still leaves the player with a locally-visible random avatar for the session (same degrade-gracefully convention as wallet/fuel hydration) and will simply retry the assignment next time the profile is hydrated with an empty `AvatarId`.

### 7. `AvatarSelectionModal` (new, `UI/`, mirrors `DisplayNameModal`)

```csharp
[SerializeField] private Transform      _gridContainer;
[SerializeField] private GameObject     _avatarButtonPrefab;  // Image + Button + selection-highlight frame
[SerializeField] private Button         _cancelButton;
[SerializeField] private TMP_Text       _statusText;

[Inject] private PlayerState      _playerState;
[Inject] private ProfileService   _profiles;
[Inject] private DatabaseRegistry _registry;
```

`Open()`: instantiates one button per `_registry.AllAvatars` entry into `_gridContainer` (built once on first `Open()`, not re-built every time), sets each button's icon to `AvatarDefinition.Sprite`, highlights the entry matching `_playerState.AvatarId`. Clicking an avatar button immediately calls `_profiles.UpdateAvatarAsync(id)` (no separate "confirm" step, since a grid tap is already the selection — same immediacy as tapping a hex tile, not a form to fill in); on success, `_playerState.SetAvatarId(id)`, update the highlight, and close. On failure (or `AVATAR_INVALID`, which shouldn't be reachable from this UI), show `_statusText` and leave the modal open. `_cancelButton` just closes without changing anything.

### 8. `HUDController` (extended)

- New serialized fields: `_avatarImage` (`Image`, shows the current avatar sprite), `_avatarButton` (wraps `_avatarImage`, opens the modal), `_avatarSelectionModal`.
- In `Start()`: `_avatarButton?.onClick.AddListener(() => _avatarSelectionModal?.Open());`, subscribe to `_playerState.OnAvatarChanged += SetAvatar;`, call `SetAvatar(_playerState.AvatarId)` once immediately (same initial-sync pattern as `SetUsername`).
- `SetAvatar(string avatarId)`: looks up `_registry.GetAvatar(avatarId)?.Sprite` and assigns it to `_avatarImage.sprite`; no-ops if not yet resolved (empty at first frame, before hydration completes).
- Unsubscribe in `OnDestroy()` alongside the existing unsubscribes.

## Data Flow

**New signup, first Planet-scene load:** `RegisterAsync` → Planet scene → `HydrateServerStateAsync` fetches profile → `avatarId` is null → a random `AvatarDefinition` is picked, applied to `PlayerState` immediately, and persisted via `UpdateAvatarAsync` in the background → `HUDController.SetAvatar` renders it as soon as `OnAvatarChanged` fires.

**Returning player:** same hydration path, but `profile.AvatarId` is already set, so it's applied directly with no random pick or write.

**Changing avatar:** HUD avatar icon tap → `AvatarSelectionModal.Open()` → tap a different avatar → `UpdateAvatarAsync` → `PlayerState.SetAvatarId` → HUD icon updates via the event → modal closes.

## Error Handling

- Hydration-time random assignment: persist failure is non-fatal (logged, local pick still shown, self-heals on next hydration) — same convention as wallet/fuel.
- Modal-time selection: persist failure keeps the modal open with a status message and does not change `PlayerState` (the player is still looking at their old avatar as selected, which is correct — the change didn't take effect).
- Server-side `AVATAR_INVALID` is a defensive guard against a stale client catalog (e.g. an old build offering an avatar id removed from a later `DatabaseRegistry`), not an expected path in normal play.

## Testing

- EditMode tests for `ProfileService.UpdateAvatarAsync` against a fake `IBackendClient` (mirrors any existing `UpdateDisplayNameAsync` coverage) and `DatabaseRegistry.GetAvatar`/`AllAvatars`.
- EditMode test for the `HydrateServerStateAsync` random-assignment branch: empty `AvatarId` → a catalog id gets assigned and `UpdateAvatarAsync` is called; non-empty `AvatarId` → passed through untouched and `UpdateAvatarAsync` is *not* called.
- No automated coverage for `AvatarSelectionModal`/`HUDController` (consistent with this codebase's convention — MonoBehaviour UI is manually verified in Play Mode, not unit tested).
- Manual verification checklist (Play Mode, mock backend): fresh signup → confirm a random avatar appears in the HUD without any user action → open the picker → confirm all 25 avatars render with the current one highlighted → pick a different one → confirm the HUD updates and the choice survives a scene reload → sign out and back in → confirm the same avatar is still shown (persisted, not re-randomized).
