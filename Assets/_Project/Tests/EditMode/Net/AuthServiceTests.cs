using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // AuthService is a thin Firebase/UGS-OIDC wrapper verified in PlayMode. Its sign-in does use
    // Cloud Code for one thing — claiming the account's single active session (ClaimSession) — so
    // it is built with the IBackendClient that stamps that session on every later call.
    public class AuthServiceTests
    {
        [Test]
        public void AuthService_constructs_with_the_backend_client_it_claims_sessions_through()
        {
            Assert.DoesNotThrow(() => { var _ = new AuthService(new FakeSocialBackendClient()); });
        }
    }
}
