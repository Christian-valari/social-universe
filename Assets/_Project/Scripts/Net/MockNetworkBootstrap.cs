using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public class MockNetworkBootstrap : INetworkBootstrap
    {
        public bool IsInitialized { get; private set; }

        public async Task InitializeAsync()
        {
            await Task.Delay(200); // simulate init latency
            IsInitialized = true;
            SULog.Info("[MOCK] Network initialized", SULog.Channel.Net);
        }
    }
}
