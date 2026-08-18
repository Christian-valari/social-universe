using System.Threading.Tasks;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // Public top-level DTO so IBackendClient.CallAsync<DroneActionResult> can type the response.
    // Fleet MUST MATCH the { fleet } payload returned by the drone ServerCode functions.
    public class DroneActionResult
    {
        public bool               Success;
        public string             Reason;
        public int                NewBalance = -1;
        public DroneFleetSnapshot Fleet;
    }

    public interface IDroneService
    {
        Task<DroneActionResult> AcquireDroneAsync(string droneId);
        Task<DroneActionResult> UnlockSlotAsync();
        Task<DroneActionResult> UpgradeAsync(string droneId, DroneStat stat);
        Task<DroneActionResult> SetActiveAsync(string droneId);
    }
}
