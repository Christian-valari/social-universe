namespace SocialUniverse.Mining
{
    // Public DTO so IBackendClient.CallAsync<MiningGrantResult> can type the response (the
    // public-DTO testability pattern, like SellResult). Shape MUST MATCH ServerCode/ValidateMining.js:
    // { granted, mineralId } on success, { granted: 0, mineralId, reason } on rejection.
    public class MiningGrantResult
    {
        public int    Granted;
        public string MineralId;
        public string Reason;
    }
}
