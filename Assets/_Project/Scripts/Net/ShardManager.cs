using System;
using System.Threading.Tasks;
using SocialUniverse.Config;
using SocialUniverse.Core;
using Unity.Services.Multiplayer;

namespace SocialUniverse.Net
{
    // Owns the Multiplayer Sessions lifecycle for planet shards. A shard is a
    // session with a deterministic id ({planetId}_shard{N}); joining a planet
    // walks shard 0..MaxShardsPerPlanet-1 until CreateOrJoinSessionAsync
    // succeeds, so players fill shards in order. Relay networking is attached
    // so Netcode for GameObjects can replicate NetworkPlayer markers.
    public class ShardManager
    {
        public const string DisplayNamePropertyKey = "displayName";

        private readonly SocialConfig _config;
        private readonly IAuthService _auth;

        public ISession CurrentSession { get; private set; }
        public string   CurrentShardId => CurrentSession?.Id;

        // Raised after the current session changes (joined or left).
        public event Action SessionChanged;

        public ShardManager(SocialConfig config, IAuthService auth)
        {
            _config = config;
            _auth   = auth;
        }

        public static string ShardId(string planetId, int shardIndex) =>
            $"{planetId.ToLowerInvariant()}_shard{shardIndex}";

        public async Task<bool> JoinPlanetAsync(string planetId)
        {
            for (int i = 0; i < _config.MaxShardsPerPlanet; i++)
            {
                if (await TryJoinAsync(ShardId(planetId, i))) return true;
            }
            SULog.Warn($"ShardManager: all shards full for {planetId}", SULog.Channel.Net);
            return false;
        }

        public Task<bool> JoinShardAsync(string shardId) => TryJoinAsync(shardId);

        public async Task LeaveAsync()
        {
            if (CurrentSession == null) return;
            try
            {
                await CurrentSession.LeaveAsync();
            }
            catch (Exception ex)
            {
                SULog.Warn($"ShardManager: leave failed ({ex.Message})", SULog.Channel.Net);
            }
            CurrentSession = null;
            SessionChanged?.Invoke();
        }

        private async Task<bool> TryJoinAsync(string shardId)
        {
            await LeaveAsync();

            var options = new SessionOptions
            {
                Name       = shardId,
                MaxPlayers = _config.MaxPlayersPerShard
            }.WithRelayNetwork();

            options.PlayerProperties[DisplayNamePropertyKey] =
                new PlayerProperty(_auth.IsSignedIn ? _auth.PlayerId : "anonymous");

            try
            {
                CurrentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(shardId, options);
                SULog.Info($"ShardManager: joined shard {shardId} ({CurrentSession.PlayerCount}/{CurrentSession.MaxPlayers})", SULog.Channel.Net);
                SessionChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                SULog.Warn($"ShardManager: join {shardId} failed ({ex.Message})", SULog.Channel.Net);
                return false;
            }
        }
    }
}
