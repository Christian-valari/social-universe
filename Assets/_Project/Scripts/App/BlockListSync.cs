using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;

namespace SocialUniverse.App
{
    // Pulls the signed-in player's block list from Cloud Save ("blocked_users": written only by the
    // BlockUser Cloud Code script, readable by the player) into ReportService, so chat filtering
    // matches the server on every device. App-layer because Social doesn't reference Net. A failed
    // read throws and leaves the list unloaded, so the Planet scene's data pull fetches it again.
    public class BlockListSync
    {
        private readonly ICloudSave    _cloudSave;
        private readonly ReportService _reports;

        public BlockListSync(ICloudSave cloudSave, ReportService reports)
        {
            _cloudSave = cloudSave;
            _reports   = reports;
        }

        public async Task LoadAsync()
        {
            var blocked = await _cloudSave.LoadAsync<List<string>>(SaveKeys.BlockedUsers, null);
            _reports.SetBlockList(blocked);
        }
    }
}
