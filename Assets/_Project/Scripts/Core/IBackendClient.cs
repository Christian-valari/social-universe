using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    // Contract for Cloud Code RPC calls. Lives in Core (like IAuthService and
    // INetworkBootstrap) so gameplay assemblies (Economy, Social) can call
    // server functions without referencing SocialUniverse.Net; the UGS-backed
    // implementation stays in Net.
    public interface IBackendClient
    {
        // The account's active session on this device (ClaimSession.js), sent with every call;
        // set by AuthService on sign-in, cleared on sign-out or when the server reports
        // SESSION_REPLACED (see SessionReplacedEvent).
        string SessionId { get; set; }

        Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null);
        Task    CallAsync(string function, Dictionary<string, object> args = null);
    }
}
