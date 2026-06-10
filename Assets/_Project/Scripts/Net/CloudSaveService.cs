using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
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

        public async Task<T> LoadAsync<T>(string key, T defaultValue = default)
        {
            try
            {
                var keys   = new HashSet<string> { key };
                var result = await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (result.TryGetValue(key, out var item))
                    return item.Value.GetAs<T>();
            }
            catch (CloudSaveException ex) when (ex.Reason == CloudSaveExceptionReason.NotFound)
            {
                // Key doesn't exist yet — return default silently.
            }
            catch (Exception ex)
            {
                SULog.Warn($"CloudSave: failed to load '{key}': {ex.Message}", SULog.Channel.Net);
            }

            return defaultValue;
        }

        public async Task DeleteAsync(string key)
        {
            await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.DeleteAsync(key);
            SULog.Info($"CloudSave: deleted '{key}'", SULog.Channel.Net);
        }
    }
}
