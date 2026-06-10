using System.Threading.Tasks;
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

            IsInitialized = true;
            SULog.Info($"UGS initialized (env: {envName})", SULog.Channel.Net);
        }
    }
}
