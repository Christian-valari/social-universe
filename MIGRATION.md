# Migration: Netcode/Relay/Lobby → Vivox-only chat + presence

**Branch:** `refactor/vivox-only-social`

## What changed

Removed the real-time networking layer and replaced it with Vivox as the
single source of both text chat and presence.

**Packages removed** (`Packages/manifest.json`):
- `com.unity.netcode.gameobjects`
- `com.unity.services.multiplayer` (Sessions/Relay — the project never had
  separate `com.unity.services.relay` / `com.unity.services.lobby` packages;
  this is the package that provided that functionality here)

**Scripts deleted:**
- `Net/NetworkPlayer.cs`, `Net/PlayerSyncController.cs` — replicated player
  marker + position sync (NGO `NetworkBehaviour`s)
- `Net/ShardManager.cs` — Multiplayer Sessions/Relay shard-join lifecycle
- `Net/ConnectionManager.cs` — registered in DI but never consumed; deleted
  as dead code rather than re-justified under the new model
- `Net/PresenceService.cs` — the NGO/session-backed `IPresenceService` impl
- `Assets/Prefabs/Net/NetworkPlayer.prefab`, `Assets/DefaultNetworkPrefabs.asset`
- The `NetworkManager`/`UnityTransport` GameObject in `Assets/Scenes/Planet.unity`

**Added:**
- `Net/VivoxPresenceService.cs` — `IPresenceService` derived from the roster
  of the planet's Vivox text channel (`VivoxService.Instance.ActiveChannels`),
  via `ChatChannelController`. Joining for chat and joining for presence are
  the same Vivox channel join — there is no separate session or host to join.

**Interface change:**
- `IPresenceService.JoinShardAsync` removed (no shards to walk).
- `IPresenceService.CurrentShardId` renamed to `CurrentChannelName`.

## Why

Social Universe has no co-located real-time gameplay: players never see each
other move, there's no synced scene, and no host. "Multiplayer" here is
persisted server-authoritative state (UGS Economy / Cloud Save / Cloud Code)
plus text chat and "who's on this planet" presence. Netcode for GameObjects,
Relay, and Lobby/Multiplayer Sessions existed only to support a player-host
replication model the design doesn't use — Vivox's channel roster already
gives presence for free on top of the chat connection the game needed anyway.

If real-time avatar movement is ever needed, that's a separate, deliberate
decision (dedicated server / authoritative match) — this migration does not
attempt to preserve a path to it.
