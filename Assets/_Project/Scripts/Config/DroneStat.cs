namespace SocialUniverse.Config
{
    // Which upgradeable drone stat an UpgradeDefinition track targets.
    // MUST MATCH the stat keys used in ServerCode/UpgradeDrone.js ("Cargo"/"Yield"/"Speed").
    public enum DroneStat
    {
        Cargo,
        Yield,
        Speed
    }
}
