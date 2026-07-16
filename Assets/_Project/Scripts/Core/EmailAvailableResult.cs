namespace SocialUniverse.Core
{
    // Response shape for the "CheckEmailAvailable" Cloud Code function. Public so
    // tests can construct it for a fake IBackendClient.
    public class EmailAvailableResult
    {
        public bool Available;
    }
}
