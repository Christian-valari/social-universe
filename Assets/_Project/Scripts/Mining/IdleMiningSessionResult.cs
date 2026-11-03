namespace SocialUniverse.Mining
{
    // Public DTO so IBackendClient.CallAsync<IdleMiningSessionResult> can type the reply (the
    // public-DTO testability pattern, like MiningGrantResult). Shape MUST MATCH
    // ServerCode/StartIdleMining.js and GetIdleMining.js:
    // { success, reason?, planetId, slotId, mineralId, elapsedSeconds, durationSeconds }.
    // ElapsedSeconds is measured on the server clock, so every device shows the same progress.
    public class IdleMiningSessionResult
    {
        public bool   Success;
        public string Reason;
        public string PlanetId;
        public string SlotId;
        public string MineralId;
        public float  ElapsedSeconds;
        public float  DurationSeconds;

        // A run in progress: a started run, the one ALREADY_MINING names, or GetIdleMining's.
        public bool HasSession => !string.IsNullOrEmpty(SlotId);
    }
}
