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
                catch (CloudCodeException ex) when (IsTransient(ex, function))
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

        // NoInternetConnection / ServiceUnavailable mean the call was not accepted, so any function
        // may retry. Unknown (e.g. a timeout) may have committed server-side, so only read-only
        // functions retry it — see BackendRetryPolicy (Known Issue #16).
        private static bool IsTransient(CloudCodeException ex, string function)
        {
            return ex.Reason switch
            {
                CloudCodeExceptionReason.NoInternetConnection => true,
                CloudCodeExceptionReason.ServiceUnavailable   => true,
                CloudCodeExceptionReason.Unknown              => BackendRetryPolicy.IsSafeToRetryAmbiguousFailure(function),
                _                                             => false
            };
        }
    }
}
