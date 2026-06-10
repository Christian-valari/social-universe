using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public class LocalMockCloudSave : ICloudSave
    {
        private readonly Dictionary<string, object> _store = new();

        public async Task SaveAsync<T>(string key, T value)
        {
            await Task.Delay(50);
            _store[key] = value;
            SULog.Info($"[MOCK] CloudSave.Save({key})", SULog.Channel.Net);
        }

        public async Task<T> LoadAsync<T>(string key, T defaultValue = default)
        {
            await Task.Delay(50);
            if (_store.TryGetValue(key, out var raw) && raw is T typed)
                return typed;
            return defaultValue;
        }

        public async Task DeleteAsync(string key)
        {
            await Task.Delay(50);
            _store.Remove(key);
        }
    }
}
