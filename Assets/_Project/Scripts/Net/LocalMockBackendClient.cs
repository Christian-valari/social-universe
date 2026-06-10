using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public class LocalMockBackendClient : IBackendClient
    {
        public async Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
        {
            await Task.Delay(100);
            SULog.Info($"[MOCK] BackendClient.CallAsync<{typeof(T).Name}>({function})", SULog.Channel.Net);
            return default;
        }

        public async Task CallAsync(string function, Dictionary<string, object> args = null)
        {
            await Task.Delay(100);
            SULog.Info($"[MOCK] BackendClient.CallAsync({function})", SULog.Channel.Net);
        }
    }
}
