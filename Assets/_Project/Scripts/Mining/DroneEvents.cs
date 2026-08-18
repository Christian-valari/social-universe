using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    // UI -> App intent events for the Drone Garage.
    public class DroneAcquireRequestedEvent     { public string DroneId; }
    public class DroneSlotUnlockRequestedEvent  { }
    public class DroneUpgradeRequestedEvent     { public string DroneId; public DroneStat Stat; }
    public class SetActiveDroneRequestedEvent   { public string DroneId; }
}
