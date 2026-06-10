using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    public interface INetworkBootstrap
    {
        bool IsInitialized { get; }
        Task InitializeAsync();
    }
}
