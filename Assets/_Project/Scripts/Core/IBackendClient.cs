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
        Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null);
        Task    CallAsync(string function, Dictionary<string, object> args = null);
    }
}
