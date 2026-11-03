using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models.Data.Player;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public class CloudSaveService : ICloudSave
    {
        public async Task SaveAsync<T>(string key, T value)
        {
            var data = new Dictionary<string, object> { { key, value } };
            await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.SaveAsync(data);
            SULog.Info($"CloudSave: saved '{key}'", SULog.Channel.Net);
        }

        public Task<T> LoadAsync<T>(string key, T defaultValue = default) =>
            LoadWithOptionsAsync(key, defaultValue, new LoadOptions(new DefaultReadAccessClassOptions()));

        public Task<T> LoadProtectedAsync<T>(string key, T defaultValue = default) =>
            LoadWithOptionsAsync(key, defaultValue, new LoadOptions(new ProtectedReadAccessClassOptions()));

        private static async Task<T> LoadWithOptionsAsync<T>(string key, T defaultValue, LoadOptions options)
        {
            try
            {
                var keys   = new HashSet<string> { key };
                var result = await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(keys, options);

                if (result.TryGetValue(key, out var item))
                    return item.Value.GetAs<T>();
            }
            catch (CloudSaveException ex) when (ex.Reason == CloudSaveExceptionReason.NotFound)
            {
                // Key doesn't exist yet — return default silently.
            }

            // Any other failure (network, auth, a record that doesn't deserialize) propagates: read as
            // "no record" it would open the game with a new player's defaults instead of the player's
            // data, and the scene's data pull (App.PlayerDataPull) retries it instead.
            return defaultValue;
        }

        public async Task DeleteAsync(string key)
        {
            await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.DeleteAsync(key);
            SULog.Info($"CloudSave: deleted '{key}'", SULog.Channel.Net);
        }
    }
}
