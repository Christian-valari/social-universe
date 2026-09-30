using System;
using VContainer.Unity;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.App
{
    // Owns the service calls for Drone Garage intents (mirrors TilePurchaseHandler). The
    // Garage view only publishes intent events; this controller performs the IDroneService call.
    public class DroneGarageHandler : IStartable, IDisposable
    {
        private readonly IDroneService _drones;

        public DroneGarageHandler(IDroneService drones) => _drones = drones;

        public void Start()
        {
            EventBus.Subscribe<DroneAcquireRequestedEvent>(OnAcquire);
            EventBus.Subscribe<DroneSlotUnlockRequestedEvent>(OnUnlockSlot);
            EventBus.Subscribe<DroneUpgradeRequestedEvent>(OnUpgrade);
            EventBus.Subscribe<SetActiveDroneRequestedEvent>(OnSetActive);
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DroneAcquireRequestedEvent>(OnAcquire);
            EventBus.Unsubscribe<DroneSlotUnlockRequestedEvent>(OnUnlockSlot);
            EventBus.Unsubscribe<DroneUpgradeRequestedEvent>(OnUpgrade);
            EventBus.Unsubscribe<SetActiveDroneRequestedEvent>(OnSetActive);
        }

        private async void OnAcquire(DroneAcquireRequestedEvent e)   { var r = await _drones.AcquireDroneAsync(e.DroneId); Report("Purchase", r); }
        private async void OnUnlockSlot(DroneSlotUnlockRequestedEvent e) { var r = await _drones.UnlockSlotAsync();        Report("Slot unlock", r); }
        private async void OnUpgrade(DroneUpgradeRequestedEvent e)   { var r = await _drones.UpgradeAsync(e.DroneId, e.Stat); Report("Upgrade", r); }
        private async void OnSetActive(SetActiveDroneRequestedEvent e) { var r = await _drones.SetActiveAsync(e.DroneId);   Report("Drone switch", r); }

        // Fleet + Wallet changes are already applied + eventful (DroneFleetChangedEvent) by the
        // service on success; a rejection is surfaced to the player as a toast (Known Issue #17).
        private static void Report(string action, DroneActionResult r)
        {
            if (r is not { Success: false }) return;
            SULog.Warn($"Drone {action} failed: {r.Reason}", SULog.Channel.Economy);
            EventBus.Publish(new ServerActionFailedEvent(ServerFailureMessages.For(action, r.Reason)));
        }
    }
}
