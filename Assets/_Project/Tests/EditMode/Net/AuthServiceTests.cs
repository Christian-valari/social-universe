using NUnit.Framework;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // AuthService is a thin Firebase/UGS-OIDC wrapper verified in PlayMode. The
    // one EditMode-checkable property is its construction contract: after the
    // Firebase migration, auth no longer routes through Cloud Code, so
    // AuthService must construct without an IBackendClient dependency.
    public class AuthServiceTests
    {
        [Test]
        public void AuthService_constructs_without_a_backend_dependency()
        {
            // Auth no longer routes through Cloud Code; construction must not require IBackendClient.
            Assert.DoesNotThrow(() => { var _ = new AuthService(); });
        }
    }
}
