using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // UI -> App intent events for the Drone Garage.
    public class DroneAcquireRequestedEvent     { public string DroneId; }
    public class DroneSlotUnlockRequestedEvent  { }
    public class DroneUpgradeRequestedEvent     { public string DroneId; public DroneStat Stat; }
    public class SetActiveDroneRequestedEvent   { public string DroneId; }

    // App -> UI: the result of a drone or slot purchase, for the Drone Garage's purchase popup.
    // DroneId is null for a slot. Message is the player-facing failure text (ServerFailureMessages),
    // "" on success.
    public class DronePurchaseCompletedEvent    { public string DroneId; public bool Success; public string Message; }
}
