using System.Threading.Tasks;
using Firebase;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using SocialUniverse.Core;
using SocialUniverse.Config;

namespace SocialUniverse.Net
{
    public class NetworkBootstrap : INetworkBootstrap
    {
        private readonly AppConfig _appConfig;

        public bool IsInitialized { get; private set; }

        public NetworkBootstrap(AppConfig appConfig)
        {
            _appConfig = appConfig;
        }

        public async Task InitializeAsync()
        {
            if (IsInitialized) return;

            var envName = _appConfig.Environment switch
            {
                AppEnvironment.Production  => "production",
                AppEnvironment.Development => "development",
                _                          => "development"
            };

            var options = new InitializationOptions().SetEnvironmentName(envName);
            await UnityServices.InitializeAsync(options);

            // Firebase is the identity source of truth; ensure its native deps are
            // present before any sign-in. On a misconfigured device this throws and
            // Bootstrap surfaces it rather than failing silently at first sign-in.
            var depStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (depStatus != DependencyStatus.Available)
                throw new System.InvalidOperationException($"Firebase dependencies unavailable: {depStatus}");

            IsInitialized = true;
            SULog.Info($"UGS + Firebase initialized (env: {envName})", SULog.Channel.Net);
        }
    }
}
