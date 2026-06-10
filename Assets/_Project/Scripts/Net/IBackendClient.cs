using System.Collections.Generic;
using System.Threading.Tasks;

namespace SocialUniverse.Net
{
    public interface IBackendClient
    {
        Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null);
        Task    CallAsync(string function, Dictionary<string, object> args = null);
    }
}
