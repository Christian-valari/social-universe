using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public class BackendClient : IBackendClient
    {
        private const int MaxRetries   = 3;
        private const int RetryDelayMs = 1000;

        public async Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
        {
            args ??= new Dictionary<string, object>();
            Exception lastEx = null;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    return await CloudCodeService.Instance.CallEndpointAsync<T>(function, args);
                }
                catch (CloudCodeException ex) when (IsTransient(ex))
                {
                    lastEx = ex;
                    SULog.Warn($"BackendClient: transient error on '{function}' (attempt {attempt + 1}/{MaxRetries}): {ex.Message}", SULog.Channel.Net);
                    await Task.Delay(RetryDelayMs * (attempt + 1));
                }
                catch (CloudCodeException ex)
                {
                    SULog.Error($"BackendClient: non-transient error on '{function}': {ex.Message}", SULog.Channel.Net);
                    throw;
                }
            }

            throw new Exception($"BackendClient: '{function}' failed after {MaxRetries} attempts", lastEx);
        }

        public async Task CallAsync(string function, Dictionary<string, object> args = null)
        {
            await CallAsync<object>(function, args);
        }

        private static bool IsTransient(CloudCodeException ex)
        {
            return ex.Reason is
                CloudCodeExceptionReason.NoInternetConnection or
                CloudCodeExceptionReason.ServiceUnavailable or
                CloudCodeExceptionReason.Unknown;
        }
    }
}
