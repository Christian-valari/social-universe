using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Net
{
    // Fetches the authoritative server timestamp via Cloud Code and keeps a local
    // offset so subsequent calls are cheap without another round-trip.
    public class ServerTime
    {
        private readonly IBackendClient _backend;

        private TimeSpan _serverOffset = TimeSpan.Zero;
        private bool     _synced;

        public ServerTime(IBackendClient backend)
        {
            _backend = backend;
        }

        public DateTime UtcNow => _synced
            ? DateTime.UtcNow + _serverOffset
            : DateTime.UtcNow;

        public async Task SyncAsync()
        {
            try
            {
                var before     = DateTime.UtcNow;
                var serverMs   = await _backend.CallAsync<long>("GetServerTime", null);
                var after      = DateTime.UtcNow;
                var rtt        = (after - before) / 2;
                var serverTime = DateTimeOffset.FromUnixTimeMilliseconds(serverMs).UtcDateTime;

                _serverOffset = serverTime - (before + rtt);
                _synced       = true;

                SULog.Info($"ServerTime synced — offset: {_serverOffset.TotalMilliseconds:0}ms", SULog.Channel.Net);
            }
            catch (Exception ex)
            {
                SULog.Warn($"ServerTime: sync failed, using local clock — {ex.Message}", SULog.Channel.Net);
            }
        }
    }
}
