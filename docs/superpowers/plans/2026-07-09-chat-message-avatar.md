# Chat Message Avatar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every row in `ChatMessageItem.prefab` shows an avatar next to the sender's name — your own real avatar (resolved once at chat-connect time via `ProfileService`), a placeholder for other senders — and the existing chat system (moderation, channel switching, DM rules, EventBus fan-out) is confirmed still working via its EditMode suite plus a manual Play Mode smoke test.

**Architecture:** Thread a new `ChatMessage.AvatarId` field through both `IChatService` implementations (populated at `ConnectAsync` time from `ProfileService`, since `PlayerState` lives in a DI scope chat can't reach — see spec §Context). `ChatMessageItemView` stays a passive view; the caller (`SocialDebugPanel`) resolves `AvatarId → Sprite` via the existing `DatabaseRegistry` and passes the sprite in. The prefab gets a new `SenderRow` (avatar + name, horizontal) replacing the bare sender-name row.

**Tech Stack:** Unity 6 / C# / VContainer DI / NUnit (EditMode tests) / UnityMCP tools for prefab editing.

## Global Constraints

- Server-authoritative economy / backend-behind-interfaces rules (CLAUDE.md) don't apply here — no economy or backend-SDK code is touched.
- Namespace/assembly: all script changes stay in their existing namespaces (`SocialUniverse.Social`, `SocialUniverse.UI`, `SocialUniverse.App`, `SocialUniverse.Tests`). No new files outside those.
- No automated tests for `ChatMessageItemView`/`SocialDebugPanel` (MonoBehaviour UI) — matches this codebase's existing convention (see `2026-07-06-avatar-selection-design.md` Testing section); verified manually instead.
- Full spec: `docs/superpowers/specs/2026-07-09-chat-message-avatar-design.md`.

---

## Task 1: Thread `AvatarId` through `ChatMessage` and `IChatService`

**Files:**
- Modify: `Assets/_Project/Scripts/Social/ChatMessage.cs`
- Modify: `Assets/_Project/Scripts/Social/IChatService.cs`
- Modify: `Assets/_Project/Scripts/Social/LocalMockChatService.cs`
- Modify: `Assets/_Project/Scripts/Social/ChatService.cs`
- Modify: `Assets/_Project/Scripts/App/SocialServicesInitializer.cs`
- Modify: `Assets/_Project/Tests/EditMode/Social/FakeSocialDoubles.cs`
- Test: `Assets/_Project/Tests/EditMode/Social/LocalMockChatServiceTests.cs` (new)

**Interfaces:**
- Consumes: `ProfileService.GetProfileAsync(string playerId) : Task<PlayerProfile>` (existing, `Assets/_Project/Scripts/Social/ProfileService.cs:35`), `PlayerProfile.AvatarId` (existing field, `Assets/_Project/Scripts/Social/PlayerProfile.cs`).
- Produces: `ChatMessage.AvatarId` (public string field), `IChatService.ConnectAsync(string displayName, string avatarId)` — both later tasks (Task 2, Task 3) read `ChatMessage.AvatarId`.

- [ ] **Step 1: Write the failing test**

Create `Assets/_Project/Tests/EditMode/Social/LocalMockChatServiceTests.cs`:

```csharp
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Social;

namespace SocialUniverse.Tests
{
    public class LocalMockChatServiceTests
    {
        private LocalMockChatService _chat;

        [SetUp]
        public void SetUp() => _chat = new LocalMockChatService();

        [Test]
        public async Task Outbound_channel_message_carries_the_connected_avatarId()
        {
            await _chat.ConnectAsync("Stella", "avatar_wizard");
            await _chat.JoinChannelAsync("global");

            ChatMessage received = null;
            _chat.MessageReceived += m => received = m;

            await _chat.SendMessageAsync("global", "hi");

            Assert.IsNotNull(received);
            Assert.AreEqual("avatar_wizard", received.AvatarId);
        }

        [Test]
        public async Task Outbound_direct_message_carries_the_connected_avatarId()
        {
            await _chat.ConnectAsync("Stella", "avatar_wizard");

            ChatMessage received = null;
            _chat.DirectMessageReceived += m => received = m;

            await _chat.SendDirectMessageAsync("ally_1", "hey");

            Assert.IsNotNull(received);
            Assert.AreEqual("avatar_wizard", received.AvatarId);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run (Unity Test Runner, EditMode, or via `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `assembly_names: ["SocialUniverse.Tests"]`, `test_names: ["SocialUniverse.Tests.LocalMockChatServiceTests"]`).

Expected: compile error — `LocalMockChatService.ConnectAsync(string)` does not have an overload taking two arguments.

- [ ] **Step 3: Add `AvatarId` to `ChatMessage`**

In `Assets/_Project/Scripts/Social/ChatMessage.cs`, add the field after `SenderDisplayName`:

```csharp
    public class ChatMessage
    {
        public string SenderId;
        public string SenderDisplayName;
        public string AvatarId;      // catalog id (DatabaseRegistry.GetAvatar); null if unresolved
        public string ChannelName;   // null for direct messages
        public string Text;
        public long   TimestampMs;   // unix ms
        public bool   FromSelf;
        public bool   IsDirect;
    }
```

- [ ] **Step 4: Change `IChatService.ConnectAsync`'s signature**

In `Assets/_Project/Scripts/Social/IChatService.cs`, replace:

```csharp
        Task ConnectAsync(string displayName);
```

with:

```csharp
        // avatarId is the caller's own resolved AvatarId (from ProfileService),
        // stamped onto every message this service originates as FromSelf.
        Task ConnectAsync(string displayName, string avatarId);
```

- [ ] **Step 5: Update `LocalMockChatService`**

In `Assets/_Project/Scripts/Social/LocalMockChatService.cs`:

Add a field next to `_displayName`:

```csharp
        private string _displayName = "MockPlayer";
        private string _avatarId;
```

Replace `ConnectAsync`:

```csharp
        public Task ConnectAsync(string displayName, string avatarId)
        {
            _displayName = string.IsNullOrEmpty(displayName) ? _displayName : displayName;
            _avatarId    = avatarId;
            IsConnected  = true;
            return Task.CompletedTask;
        }
```

In `SendMessageAsync`, add `AvatarId = _avatarId` to the constructed `ChatMessage`:

```csharp
        public Task SendMessageAsync(string channelName, string text)
        {
            if (_joinedChannels.Contains(channelName))
                MessageReceived?.Invoke(new ChatMessage
                {
                    SenderId          = MockPlayerId,
                    SenderDisplayName = _displayName,
                    AvatarId          = _avatarId,
                    ChannelName       = channelName,
                    Text              = text,
                    TimestampMs       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    FromSelf          = true
                });
            return Task.CompletedTask;
        }
```

In `SendDirectMessageAsync`, add `AvatarId = _avatarId` the same way:

```csharp
        public Task SendDirectMessageAsync(string playerId, string text)
        {
            DirectMessageReceived?.Invoke(new ChatMessage
            {
                SenderId          = MockPlayerId,
                SenderDisplayName = _displayName,
                AvatarId          = _avatarId,
                Text              = text,
                TimestampMs       = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                FromSelf          = true,
                IsDirect          = true
            });
            return Task.CompletedTask;
        }
```

- [ ] **Step 6: Update `ChatService`**

In `Assets/_Project/Scripts/Social/ChatService.cs`:

Add a field next to `_lastDisplayName`:

```csharp
        private bool _initialized;
        private string _lastDisplayName;
        private string _selfAvatarId;
```

Replace `ConnectAsync`:

```csharp
        public Task ConnectAsync(string displayName, string avatarId)
        {
            _lastDisplayName = displayName;
            _selfAvatarId    = avatarId;
            if (IsConnected) return Task.CompletedTask;
            if (_connectTask != null && !_connectTask.IsCompleted) return _connectTask;

            _connectTask = DoConnectAsync(displayName);
            return _connectTask;
        }
```

Update `EnsureConnectedAsync`'s fallback call to pass the stored avatar id through:

```csharp
        private Task EnsureConnectedAsync()
        {
            if (IsConnected) return Task.CompletedTask;
            if (_connectTask != null && !_connectTask.IsCompleted) return _connectTask;
            return ConnectAsync(_lastDisplayName ?? "Player", _selfAvatarId);
        }
```

Change `ToChatMessage` from a `static` method to an instance method so it can read `_selfAvatarId`, and stamp it only on `FromSelf` messages (Vivox's `VivoxMessage` carries no avatar concept, so other senders stay `null`, matching `LocalMockChatService`'s behavior for non-self senders):

```csharp
        private ChatMessage ToChatMessage(VivoxMessage message, bool isDirect) => new()
        {
            SenderId          = message.SenderPlayerId,
            SenderDisplayName = message.SenderDisplayName,
            AvatarId          = message.FromSelf ? _selfAvatarId : null,
            ChannelName       = message.ChannelName,
            Text              = message.MessageText,
            TimestampMs       = new DateTimeOffset(message.ReceivedTime).ToUnixTimeMilliseconds(),
            FromSelf          = message.FromSelf,
            IsDirect          = isDirect
        };
```

(`OnChannelMessage`/`OnDirectedMessage` call `ToChatMessage(message, isDirect: ...)` unchanged — no `this.` qualifier needed, C# resolves it as an instance call automatically now that it's no longer `static`.)

- [ ] **Step 7: Update the `FakeChatService` test double**

In `Assets/_Project/Tests/EditMode/Social/FakeSocialDoubles.cs`, replace:

```csharp
        public Task ConnectAsync(string displayName) { IsConnected = true;  return Task.CompletedTask; }
```

with:

```csharp
        public Task ConnectAsync(string displayName, string avatarId) { IsConnected = true;  return Task.CompletedTask; }
```

- [ ] **Step 8: Update `SocialServicesInitializer`**

In `Assets/_Project/Scripts/App/SocialServicesInitializer.cs`, add a `ProfileService` dependency and resolve the avatar id before connecting chat:

```csharp
    public class SocialServicesInitializer : IStartable, IDisposable
    {
        private readonly IChatService    _chat;
        private readonly IFriendsService _friends;
        private readonly IAuthService    _auth;
        private readonly ServerTime      _serverTime;
        private readonly ProfileService  _profile;

        // DirectMessageService subscribes to inbound DMs in its constructor;
        // depending on it here forces eager construction so DMs are captured
        // app-wide even before any chat UI resolves it.
        public SocialServicesInitializer(
            IChatService          chat,
            IFriendsService       friends,
            IAuthService          auth,
            ServerTime            serverTime,
            ProfileService        profile,
            DirectMessageService  _)
        {
            _chat       = chat;
            _friends    = friends;
            _auth       = auth;
            _serverTime = serverTime;
            _profile    = profile;
        }

        public void Start() => EventBus.Subscribe<PlayerReadyEvent>(OnPlayerReady);

        public void Dispose() => EventBus.Unsubscribe<PlayerReadyEvent>(OnPlayerReady);

        private async void OnPlayerReady(PlayerReadyEvent _)
        {
            // SyncAsync never throws (it logs and falls back to the local clock on
            // failure), so this needs no try/catch of its own.
            await _serverTime.SyncAsync();

            // Non-fatal: a failed profile fetch just means own messages fall back
            // to the placeholder avatar too, same as any other unresolved sender.
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
                SULog.Info("SocialServicesInitializer: chat connected", SULog.Channel.Social);
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: chat connect failed ({ex.Message})", SULog.Channel.Social);
            }

            try
            {
                await _friends.InitializeAsync();
            }
            catch (Exception ex)
            {
                SULog.Warn($"SocialServicesInitializer: friends init failed ({ex.Message})", SULog.Channel.Social);
            }
        }
    }
```

(`ProfileService` is already registered in `RootLifetimeScope` — `Assets/_Project/Scripts/App/RootLifetimeScope.cs:54` — same scope as `IChatService`, so this is a same-scope constructor dependency, no new DI registration needed.)

- [ ] **Step 9: Run the test to verify it passes**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `assembly_names: ["SocialUniverse.Tests"]`, `test_names: ["SocialUniverse.Tests.LocalMockChatServiceTests"]`, then `mcp__UnityMCP__get_test_job` with the returned `job_id` (`wait_timeout: 60`).

Expected: 2 passed, 0 failed. Also run the full `SocialUniverse.Tests` assembly once (no `test_names` filter) to confirm the signature change didn't break `ChatChannelControllerTests`/`DirectMessageServiceTests`/`ProfileServiceTests`/`ReportServiceTests`/`LocalMockFriendsServiceTests`/`ChatModerationFilterTests` — expected all green.

- [ ] **Step 10: Commit**

```bash
git add Assets/_Project/Scripts/Social/ChatMessage.cs \
        Assets/_Project/Scripts/Social/IChatService.cs \
        Assets/_Project/Scripts/Social/LocalMockChatService.cs \
        Assets/_Project/Scripts/Social/ChatService.cs \
        Assets/_Project/Scripts/App/SocialServicesInitializer.cs \
        Assets/_Project/Tests/EditMode/Social/FakeSocialDoubles.cs \
        Assets/_Project/Tests/EditMode/Social/LocalMockChatServiceTests.cs
git commit -m "Thread AvatarId through ChatMessage and IChatService"
```

---

## Task 2: Regression coverage — `AvatarId` survives `ChatChannelController`/`DirectMessageService`

**Files:**
- Modify: `Assets/_Project/Tests/EditMode/Social/ChatChannelControllerTests.cs`
- Modify: `Assets/_Project/Tests/EditMode/Social/DirectMessageServiceTests.cs`

**Interfaces:**
- Consumes: `ChatMessage.AvatarId` (Task 1).
- Produces: nothing new — this is a regression-safety net confirming `ChatChannelController`/`DirectMessageService` don't strip the field (neither class currently touches it; these tests should pass immediately with no further source change).

- [ ] **Step 1: Add the `ChatChannelController` test**

In `Assets/_Project/Tests/EditMode/Social/ChatChannelControllerTests.cs`, add after `Inbound_message_lands_in_history_and_on_the_EventBus`:

```csharp
        [Test]
        public async Task Inbound_message_AvatarId_survives_into_history_and_the_EventBus()
        {
            await _controller.SwitchToGlobalAsync();

            ChatMessage received = null;
            EventBus.Subscribe<ChatChannelController.ChatMessageReceivedEvent>(e => received = e.Message);

            _chat.RaiseChannelMessage(new ChatMessage
            {
                SenderId = "friend_1", SenderDisplayName = "Friend", ChannelName = "global",
                Text = "o/", AvatarId = "avatar_wizard"
            });

            Assert.AreEqual("avatar_wizard", received.AvatarId);
            Assert.AreEqual("avatar_wizard", _controller.GetHistory("global")[0].AvatarId);
        }
```

- [ ] **Step 2: Add the `DirectMessageService` test**

In `Assets/_Project/Tests/EditMode/Social/DirectMessageServiceTests.cs`, add after `Inbound_dm_is_published_on_the_EventBus`:

```csharp
        [Test]
        public void Inbound_dm_AvatarId_survives_onto_the_EventBus()
        {
            ChatMessage received = null;
            EventBus.Subscribe<DirectMessageService.DirectMessageReceivedEvent>(e => received = e.Message);

            _chat.RaiseDirectMessage(new ChatMessage
            {
                SenderId = "ally_1", Text = "o/", IsDirect = true, AvatarId = "avatar_girl_3"
            });

            Assert.AreEqual("avatar_girl_3", received.AvatarId);
        }
```

- [ ] **Step 3: Run both tests and confirm they pass**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `assembly_names: ["SocialUniverse.Tests"]`, `test_names: ["SocialUniverse.Tests.ChatChannelControllerTests", "SocialUniverse.Tests.DirectMessageServiceTests"]`.

Expected: all pass, including the two new tests, on the first run — `ChatChannelController.OnMessageReceived`/`Append` and `DirectMessageService.OnDirectMessageReceived` already pass the whole `ChatMessage` object through unmodified, so no implementation change is needed here; this step is confirmation, not red-green.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Tests/EditMode/Social/ChatChannelControllerTests.cs \
        Assets/_Project/Tests/EditMode/Social/DirectMessageServiceTests.cs
git commit -m "Add AvatarId regression coverage to chat controller/DM tests"
```

---

## Task 3: Chat UI — render the avatar (`ChatMessageItemView` + `SocialDebugPanel`)

**Files:**
- Modify: `Assets/_Project/Scripts/UI/ChatMessageItemView.cs`
- Modify: `Assets/_Project/Scripts/UI/SocialDebugPanel.cs`

**Interfaces:**
- Consumes: `ChatMessage.AvatarId` (Task 1); `DatabaseRegistry.GetAvatar(string avatarId) : AvatarDefinition` and `AvatarDefinition.Sprite` (existing, `Assets/_Project/Scripts/Config/DatabaseRegistry.cs:27`, `AvatarDefinition.cs`).
- Produces: `ChatMessageItemView.SetMessage(ChatMessage message, Sprite avatarSprite)` — Task 4 (the prefab edit) relies on the `_avatarImage` field this task adds existing on the component before it can be wired.

No automated test for this task (MonoBehaviour UI, consistent with the codebase's existing convention — see Global Constraints). Verified by a clean compile (Unity console has no errors after the edit) and the Task 5 manual smoke test.

- [ ] **Step 1: Add the avatar field and extend `SetMessage`**

In `Assets/_Project/Scripts/UI/ChatMessageItemView.cs`, replace the whole class body:

```csharp
    public class ChatMessageItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _senderText;
        [SerializeField] private TMP_Text _timestampText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private RectTransform _messageBGRect;
        [SerializeField] private Image _avatarImage;

        public void SetMessage(ChatMessage message, Sprite avatarSprite)
        {
            _senderText.alignment = message.FromSelf ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            _timestampText.alignment = message.FromSelf ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            _messageBGRect.pivot = message.FromSelf ? new Vector2(1,1) : Vector2.zero ;
            _senderText.text    = message.FromSelf ? "Me" : message.SenderDisplayName;
            _messageText.text   = message.Text;
            _timestampText.text = message.TimestampMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(message.TimestampMs).LocalDateTime.ToString("HH:mm")
                : "--:--";

            // Null means unresolved (no catalog match / no id yet) — leave the
            // prefab's inspector-default placeholder sprite in place.
            if (avatarSprite != null) _avatarImage.sprite = avatarSprite;
        }
    }
```

(`UnityEngine.UI` is already `using` at the top of this file, so `Image` resolves without a new import.)

- [ ] **Step 2: Update the caller in `SocialDebugPanel`**

In `Assets/_Project/Scripts/UI/SocialDebugPanel.cs`, add the injected registry next to the other `[Inject]` fields:

```csharp
        [Inject] private ChatChannelController _chat;
        [Inject] private IPresenceService _presence;
        [Inject] private PlanetDefinition _planet;
        [Inject] private DatabaseRegistry _registry;
```

Then update `RefreshChatLog`'s instantiation loop:

```csharp
            int start = Mathf.Max(0, history.Count - MaxLogLines);
            for (int i = start; i < history.Count; i++)
            {
                var item = Instantiate(_chatMessageItemPrefab, _chatLogContent);
                var avatar = _registry.GetAvatar(history[i].AvatarId);
                item.SetMessage(history[i], avatar?.Sprite);
            }
```

(`SocialUniverse.Config` is already `using` at the top of this file, so `DatabaseRegistry` resolves without a new import. `SocialDebugPanel` is registered in the scene hierarchy under `PlanetSceneScope`'s VContainer container, and `DatabaseRegistry` is already a standing DI registration reachable from that scope — no new DI wiring needed.)

- [ ] **Step 3: Verify a clean compile**

After both edits, use `mcp__UnityMCP__read_console` with `types: ["error"]` (or trigger a refresh via `mcp__UnityMCP__refresh_unity` first if the Editor hasn't already recompiled) to confirm no compile errors were introduced.

Expected: no `error`-type console entries referencing `ChatMessageItemView.cs` or `SocialDebugPanel.cs`.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/UI/ChatMessageItemView.cs \
        Assets/_Project/Scripts/UI/SocialDebugPanel.cs
git commit -m "Render sender avatar in chat message rows"
```

---

## Task 4: Prefab structural edit — add the avatar image to `ChatMessageItem.prefab`

**Files:**
- Modify: `Assets/Prefabs/UI/ChatMessageItem.prefab`

**Interfaces:**
- Consumes: `ChatMessageItemView._avatarImage` (Task 3 — this task's `SerializedObject` wiring will fail loudly if that field doesn't exist yet, which is the intended guard against running this task out of order).
- Produces: the prefab's new `SenderRow`/`AvatarImage` hierarchy — no other task depends on the exact GameObject names, only on `ChatMessageItemView._avatarImage` being wired, which this task does.

This is a structural prefab edit, not a script change, so there's no NUnit test — it's done via a single Unity Editor script run through `mcp__UnityMCP__execute_code`, which loads the prefab contents, mutates the hierarchy, wires the serialized field, and saves — the Unity-native equivalent of a "write it, run it, verify it" step for a prefab asset.

- [ ] **Step 1: Run the prefab edit**

Run `mcp__UnityMCP__execute_code` with `action: "execute"` and this `code`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Linq;

string prefabPath = "Assets/Prefabs/UI/ChatMessageItem.prefab";
GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
try
{
    if (root.transform.Find("SenderRow") != null)
        throw new System.Exception("SenderRow already exists on this prefab — this edit has already been applied.");

    Transform senderText = root.transform.Find("Text (TMP)");
    if (senderText == null)
        throw new System.Exception("Could not find the 'Text (TMP)' (sender name) child under the prefab root.");

    int senderIndex = senderText.GetSiblingIndex();

    // New horizontal row replacing the bare sender-name row: [avatar][sender name].
    GameObject senderRow = new GameObject("SenderRow", typeof(RectTransform));
    senderRow.transform.SetParent(root.transform, false);
    senderRow.transform.SetSiblingIndex(senderIndex);

    var hlg = senderRow.AddComponent<HorizontalLayoutGroup>();
    hlg.padding = new RectOffset(0, 0, 0, 0);
    hlg.spacing = 8f;
    hlg.childAlignment = TextAnchor.UpperLeft;
    hlg.childForceExpandWidth = true;   // sender text expands to fill leftover width
    hlg.childForceExpandHeight = false;
    hlg.childControlWidth = true;
    hlg.childControlHeight = true;
    hlg.childScaleWidth = false;
    hlg.childScaleHeight = false;

    // Avatar image: fixed 32x32 via LayoutElement, which overrides the row's
    // ChildForceExpandWidth for this one child so it doesn't stretch.
    GameObject avatarGO = new GameObject("AvatarImage", typeof(RectTransform));
    avatarGO.transform.SetParent(senderRow.transform, false);
    avatarGO.transform.SetSiblingIndex(0);

    var avatarImage = avatarGO.AddComponent<Image>();
    string spritePath = AssetDatabase.GUIDToAssetPath("f66925c350c3642838a7bee8b9989c65"); // Empty Gray.png — generic placeholder silhouette
    avatarImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
    avatarImage.preserveAspect = true;

    var avatarLayout = avatarGO.AddComponent<LayoutElement>();
    avatarLayout.minWidth = 32f;
    avatarLayout.minHeight = 32f;
    avatarLayout.preferredWidth = 32f;
    avatarLayout.preferredHeight = 32f;
    avatarLayout.flexibleWidth = 0f;
    avatarLayout.flexibleHeight = 0f;

    // Move the existing sender-name text into the new row, after the avatar.
    senderText.SetParent(senderRow.transform, false);
    senderText.SetSiblingIndex(1);

    // Wire ChatMessageItemView._avatarImage (added in Task 3) to the new Image.
    var view = root.GetComponents<MonoBehaviour>().FirstOrDefault(c => c.GetType().Name == "ChatMessageItemView");
    if (view == null)
        throw new System.Exception("ChatMessageItemView component not found on the prefab root.");

    var so = new SerializedObject(view);
    var prop = so.FindProperty("_avatarImage");
    if (prop == null)
        throw new System.Exception("_avatarImage SerializedProperty not found — was Task 3's script change applied and compiled first?");
    prop.objectReferenceValue = avatarImage;
    so.ApplyModifiedProperties();

    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    return $"OK: SenderRow inserted at sibling index {senderIndex}, placeholder sprite={spritePath}, ChatMessageItemView._avatarImage wired.";
}
finally
{
    PrefabUtility.UnloadPrefabContents(root);
}
```

Expected return value: `"OK: SenderRow inserted at sibling index 0, placeholder sprite=Assets/Plugins/UltimateCleanGUIPack/Common/Sprites/Demo/Avatars/Empty Gray.png, ChatMessageItemView._avatarImage wired."`

- [ ] **Step 2: Verify the resulting structure**

Run `mcp__UnityMCP__manage_prefabs` with `action: "get_hierarchy"`, `target: "Assets/Prefabs/UI/ChatMessageItem.prefab"`.

Expected: root `ChatMessageItem` has children in order `SenderRow, Holder, Text (TMP) (1)`; `SenderRow` has children in order `AvatarImage, Text (TMP)`; `AvatarImage` has an `Image` and a `LayoutElement` component.

- [ ] **Step 3: Confirm no console errors**

Run `mcp__UnityMCP__read_console` with `types: ["error"]`.

Expected: no new error entries referencing `ChatMessageItem.prefab`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Prefabs/UI/ChatMessageItem.prefab"
git commit -m "Add avatar image row to ChatMessageItem prefab"
```

---

## Task 5: Full verification — test suite + manual Play Mode smoke test

**Files:** none (verification only).

**Interfaces:** consumes everything from Tasks 1-4; produces nothing further.

- [ ] **Step 1: Run the full EditMode Social suite**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `assembly_names: ["SocialUniverse.Tests"]` (no `test_names` filter, so the whole assembly runs — includes `ChatModerationFilterTests`, `ChatChannelControllerTests`, `DirectMessageServiceTests`, `ProfileServiceTests`, `ReportServiceTests`, `LocalMockFriendsServiceTests`, `LocalMockChatServiceTests`), then `mcp__UnityMCP__get_test_job` with `wait_timeout: 60`.

Expected: 0 failed. If anything outside this plan's changed files fails, stop and investigate before continuing — a regression here means something this plan touched broke an unrelated test, not that the unrelated test was already failing (the research at spec time found this suite fully green).

- [ ] **Step 2: Enter Play Mode and reach the chat panel**

Open `Assets/Scenes/Bootstrap.unity` and confirm its `RootLifetimeScope` component has `_devMode` checked (so `LocalMockChatService`/`LocalMockAuthService`/etc. are used — no live UGS/Vivox dependency). Enter Play Mode (`mcp__UnityMCP__manage_editor` action `play`). Let the flow proceed through Auth (anonymous/mock sign-in) to the `Planet` scene load. Locate the chat button on the HUD (`HUDController._chatButton`) and open it — this activates `SocialDebugPanel`.

Expected: the panel opens showing "Channel: (none)" or similar, then joins the planet channel; no console errors during the transition (`mcp__UnityMCP__read_console`, `types: ["error"]`).

- [ ] **Step 3: Send a message as self and confirm your avatar renders**

Type a message into the panel's input field and send it (toggling the global-channel switch first if needed, matching the panel's existing `_globalChannelButton` flow).

Expected: the message row appears with the message on the right (`FromSelf` pivot flip), sender name "Me", and the `AvatarImage` showing your actual avatar sprite (whatever `PlayerState.AvatarId`/`ProfileService` resolved for the mock player) — not the gray placeholder — confirming the `SocialServicesInitializer → ConnectAsync → LocalMockChatService → SocialDebugPanel → DatabaseRegistry` chain from Tasks 1 and 3 works end-to-end.

- [ ] **Step 4: Simulate an incoming message and confirm the placeholder renders**

While still in Play Mode (must run while Play Mode is active, since this resolves live scene state), run `mcp__UnityMCP__execute_code` with `action: "execute"` and this `code` (this must run while the Play Mode session from Step 2/3 is still active — `PlanetSceneScope` is a scene-local `VContainer.Unity.LifetimeScope`, whose public `Container` resolves the same singletons the running game is using):

```csharp
using UnityEngine;
using SocialUniverse.App;
using SocialUniverse.Social;

var scope = Object.FindFirstObjectByType<PlanetSceneScope>();
if (scope == null) return "FAIL: no PlanetSceneScope found in the scene — is Play Mode active and past the Planet scene load?";

var chat = scope.Container.Resolve<IChatService>() as LocalMockChatService;
if (chat == null) return "FAIL: resolved IChatService is not a LocalMockChatService — is RootLifetimeScope._devMode enabled?";

var channelController = scope.Container.Resolve<ChatChannelController>();
if (string.IsNullOrEmpty(channelController.ActiveChannel))
    return "FAIL: ChatChannelController.ActiveChannel is empty — send a message first (Step 3) so a channel is joined.";

chat.SimulateIncoming(new ChatMessage
{
    SenderId          = "test_other_player",
    SenderDisplayName = "Explorer42",
    ChannelName       = channelController.ActiveChannel,
    Text              = "hello from another explorer",
    FromSelf          = false,
    AvatarId          = null
});

return $"OK: simulated inbound message on channel '{channelController.ActiveChannel}'";
```

Expected return: `"OK: simulated inbound message on channel '<channel>'"`. Then visually confirm in the Game view: a new row appears on the left with sender name "Explorer42" and the `AvatarImage` showing the gray placeholder sprite (`Empty Gray.png`) — confirming the null-`AvatarId` fallback path from Task 3 works, and that the row layout (avatar + name row above the bubble) doesn't break for left-aligned (`FromSelf = false`) messages.

- [ ] **Step 5: Stop Play Mode and record the result**

Run `mcp__UnityMCP__manage_editor` action `stop`. Note the outcome of Steps 1-4 in the task/PR description: EditMode suite result, and pass/fail for each of the two manual checks (own avatar renders; placeholder renders for an unresolved sender). No commit for this task — it's verification-only.
