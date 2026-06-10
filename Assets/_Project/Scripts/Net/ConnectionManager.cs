using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    public enum ConnectionState { Disconnected, Connecting, Connected, Offline }

    public class ConnectionManager
    {
        private readonly INetworkBootstrap _bootstrap;
        private readonly IAuthService      _auth;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public event Action<ConnectionState> OnStateChanged;

        public ConnectionManager(INetworkBootstrap bootstrap, IAuthService auth)
        {
            _bootstrap = bootstrap;
            _auth      = auth;
        }

        public async Task ConnectAsync()
        {
            if (State == ConnectionState.Connected) return;

            SetState(ConnectionState.Connecting);

            try
            {
                if (!_bootstrap.IsInitialized)
                    await _bootstrap.InitializeAsync();

                if (!_auth.IsSignedIn)
                    await _auth.SignInAnonymouslyAsync();

                SetState(ConnectionState.Connected);
            }
            catch (Exception ex)
            {
                SULog.Warn($"ConnectionManager: connect failed, going offline — {ex.Message}", SULog.Channel.Net);
                SetState(ConnectionState.Offline);
            }
        }

        public async Task ReconnectAsync()
        {
            SetState(ConnectionState.Disconnected);
            await ConnectAsync();
        }

        private void SetState(ConnectionState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
            SULog.Info($"ConnectionManager: {state}", SULog.Channel.Net);
        }
    }
}
