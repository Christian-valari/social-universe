# Chat Message Avatar — Design

## Context

`Assets/Prefabs/UI/ChatMessageItem.prefab` (backed by `ChatMessageItemView`) renders each chat row — sender name, timestamp, message bubble — but has no avatar. The avatar system itself already exists and ships (`AvatarDefinition`, `DatabaseRegistry.GetAvatar`, `PlayerState.AvatarId`, picked via `AvatarSelectionModal`, shown in `HUDController`) — see `2026-07-06-avatar-selection-design.md`. This design wires that existing catalog into chat rows.

`ChatMessage` (the provider-agnostic DTO both `LocalMockChatService` and `ChatService` produce) currently carries no avatar reference at all — only `SenderId`/`SenderDisplayName`. Closest precedent for "showing another player's avatar" is `TileInfoModal`, which has a resolution block *commented out*; this design doesn't investigate why that was disabled, it solves the narrower "chat" case directly.

**Key constraint:** `LocalMockChatService`/`ChatService` are registered in the **Root** VContainer scope (`RootLifetimeScope`, alive from Bootstrap/Auth, spans scenes), while `PlayerState`/`AvatarId` live in **per-scene** scopes (`PlanetSceneScope`, etc.) that don't exist yet when chat connects. A parent-scope singleton cannot resolve a child-scope service, so chat cannot inject `PlayerState` directly. `ProfileService` — the server-authoritative source `PlayerState.AvatarId` is itself hydrated from — **is** Root-scoped alongside chat, so it's the correct source here instead.

**Scope for this design:**
- Every message row shows an avatar (not self-only), per-your-answer.
- Only the local player's own avatar is reliably resolvable right now (via `ProfileService` at chat-connect time). Other senders render a placeholder (the prefab's inspector-default sprite) until a real per-sender lookup (e.g. via the presence roster) is built — deliberately left as a follow-up, same pattern as the presence-roster gap called out in the avatar-selection design.
- `ChatMessageItem.prefab` is currently only instantiated by `SocialDebugPanel` (an explicit M4 dev/QA tool, not the future M11 `ChatScreen`). This design wires the avatar through that panel; a real `ChatScreen` will reuse `ChatMessageItemView.SetMessage` the same way when it's built.
- Also covers: confirming the existing chat system (moderation, channel switching, DM rules, EventBus fan-out) still works via its existing EditMode suites, plus a manual in-editor smoke test of the mock chat flow including the new avatar rendering. Does **not** cover Known Issue #7 (PlayMode bootstrap failure) or enabling/testing live Vivox — both pre-existing, separately-tracked, out of scope here.

## Goal

Every row in the chat log shows a small avatar image next to the sender's name. Your own messages show your actual chosen avatar (resolved once, at chat-connect time, from your server profile). Other players' messages show a placeholder until per-sender resolution exists. The existing chat test suites still pass and the mock chat flow still works correctly end-to-end in-editor.

## Components

### 1. `ChatMessage` (extended, `Social/`)

```csharp
public string AvatarId;
```

Added alongside the existing fields. Null/unset unless a service populates it (see below).

### 2. `IChatService.ConnectAsync` (signature change, `Social/`)

```csharp
Task ConnectAsync(string displayName, string avatarId);
```

Both implementations store the avatar id the same way they already store the display name, and stamp it onto messages they originate locally (`FromSelf = true`).

### 3. `LocalMockChatService` (extended)

```csharp
private string _avatarId;

public Task ConnectAsync(string displayName, string avatarId)
{
    _displayName = string.IsNullOrEmpty(displayName) ? _displayName : displayName;
    _avatarId    = avatarId;
    IsConnected  = true;
    return Task.CompletedTask;
}
```

`SendMessageAsync`/`SendDirectMessageAsync` set `AvatarId = _avatarId` on the `ChatMessage` they construct, alongside the existing `SenderId`/`SenderDisplayName`. `SimulateIncoming` is unchanged — callers (tests, or a future "other player" simulation) already construct the full `ChatMessage` themselves and can set `AvatarId` explicitly, or leave it null to exercise the placeholder path.

### 4. `ChatService` (extended, Vivox-backed)

```csharp
private string _selfAvatarId;

public Task ConnectAsync(string displayName, string avatarId)
{
    _lastDisplayName = displayName;
    _selfAvatarId    = avatarId;
    ...
}
```

`ToChatMessage` becomes an instance method (was `static`) so it can read `_selfAvatarId`:

```csharp
private ChatMessage ToChatMessage(VivoxMessage message, bool isDirect) => new()
{
    SenderId          = message.SenderPlayerId,
    SenderDisplayName = message.SenderDisplayName,
    ChannelName       = message.ChannelName,
    Text              = message.MessageText,
    TimestampMs       = new DateTimeOffset(message.ReceivedTime).ToUnixTimeMilliseconds(),
    FromSelf          = message.FromSelf,
    IsDirect          = isDirect,
    AvatarId          = message.FromSelf ? _selfAvatarId : null
};
```

Vivox's `VivoxMessage` has no avatar concept, so incoming messages from other players stay `AvatarId = null` regardless of provider — symmetric with the mock service's behavior for non-self senders.

### 5. `SocialServicesInitializer` (extended, `App/`)

Gains a `ProfileService` dependency. Before connecting chat, fetches the local player's profile (non-fatal — same try/catch convention already used for friends init) and passes the resolved avatar id through:

```csharp
string avatarId = null;
try
{
    var profile = await _profile.GetProfileAsync(_auth.PlayerId);
    avatarId = profile?.AvatarId;
}
catch (Exception ex)
{
    SULog.Warn($"SocialServicesInitializer: profile fetch for avatar failed ({ex.Message})", SULog.Channel.Social);
}

try
{
    await _chat.ConnectAsync(_auth.DisplayName ?? _auth.Username ?? _auth.PlayerId, avatarId);
    ...
}
```

A failed/empty fetch just means own messages fall back to the placeholder too — no crash, no blocked chat connect.

### 6. `FakeChatService` (test double, `FakeSocialDoubles.cs`)

Signature updated to match `IChatService.ConnectAsync(string, string)`; existing test call sites pass `null` or a literal avatar id as needed.

### 7. `ChatMessageItem.prefab` (structural change)

The current bare sender-name row becomes a small horizontal row: `[AvatarImage (32×32, plain `Image`, no mask — same unmasked treatment `HUDController._avatarImage` already uses)]` then the existing sender-name `TMP_Text`, left-aligned, small spacing. Everything else (message bubble, timestamp, the `FromSelf` left/right pivot flip) is unchanged. `AvatarImage`'s Inspector-assigned sprite is the placeholder shown whenever a message's avatar can't be resolved.

### 8. `ChatMessageItemView` (extended, `UI/`)

```csharp
[SerializeField] private Image _avatarImage;

public void SetMessage(ChatMessage message, Sprite avatarSprite)
{
    ... // existing sender/timestamp/message/alignment logic, unchanged
    if (avatarSprite != null) _avatarImage.sprite = avatarSprite;
}
```

Stays a passive view — no `DatabaseRegistry`/DI lookup inside it. The caller resolves the sprite and passes it in, same "dumb view, smart caller" shape the rest of this class already follows. When `avatarSprite` is null (id unresolved or avatar not found in the catalog), the prefab's inspector-default placeholder sprite is left untouched.

### 9. `SocialDebugPanel` (extended, `UI/`)

```csharp
[Inject] private DatabaseRegistry _registry;
```

In `RefreshChatLog()`, per message:

```csharp
var avatar = _registry.GetAvatar(history[i].AvatarId);
item.SetMessage(history[i], avatar?.Sprite);
```

`SocialDebugPanel` is scene-hierarchy-registered with VContainer already (`[Inject]` fields work today for `ChatChannelController`/`IPresenceService`/`PlanetDefinition`), so adding `DatabaseRegistry` is a one-line addition, no new wiring.

## Data Flow

**Own message, mock or real backend:** sign-in completes → `SocialServicesInitializer.OnPlayerReady` fetches the player's profile → `ConnectAsync(displayName, avatarId)` stores it on the service → every message that service originates as `FromSelf = true` carries that `AvatarId` → `SocialDebugPanel` resolves it to a `Sprite` via `DatabaseRegistry` → row renders your actual avatar.

**Other player's message:** `AvatarId` is null on the incoming `ChatMessage` (both providers) → `DatabaseRegistry.GetAvatar(null)` returns null → `ChatMessageItemView` leaves the prefab's placeholder sprite in place.

## Error Handling

- Profile fetch failure at connect time: non-fatal, logged, chat still connects, own messages just render the placeholder too (degrades the same way a missing `AvatarId` already does for any other sender).
- Unresolved/unknown `AvatarId` (stale catalog, deleted avatar asset): `DatabaseRegistry.GetAvatar` returns null → placeholder, no exception — same null-safe convention as `HUDController.SetAvatar`.

## Testing

- `ChatChannelControllerTests`/`DirectMessageServiceTests`: extend the existing "inbound message → history + EventBus" cases to assert `AvatarId` round-trips unchanged through `ChatChannelController`/`DirectMessageService` (they don't touch it, just pass the `ChatMessage` through — a regression here would mean something started stripping fields).
- `LocalMockChatServiceTests` (or wherever mock coverage lives, may be new): `ConnectAsync(name, avatarId)` then `SendMessageAsync` → resulting `ChatMessage.AvatarId` matches what was connected with.
- Run the full existing EditMode chat suite (`ChatModerationFilterTests`, `ChatChannelControllerTests`, `DirectMessageServiceTests`, `ProfileServiceTests`) to confirm no regressions from the `ConnectAsync` signature change.
- Manual Play Mode smoke test (mock backend, via `SocialDebugPanel`): send a message as self → confirm your real avatar renders; use `LocalMockChatService.SimulateIncoming` (or the existing dev-harness path) to inject a message from another sender → confirm the placeholder renders; confirm the `FromSelf` left/right bubble alignment still works correctly with the new row layout.
- No automated coverage for `ChatMessageItemView`/`SocialDebugPanel` themselves — consistent with this codebase's existing convention that MonoBehaviour UI is manually verified, not unit tested (see `2026-07-06-avatar-selection-design.md` Testing section).
