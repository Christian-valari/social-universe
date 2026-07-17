using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Net;

namespace SocialUniverse.Tests
{
    // AuthService is a thin UGS/Cloud-Code wrapper verified in PlayMode. The one
    // piece of client-side branching that does NOT touch UGS singletons is
    // IsEmailAvailableAsync's null-payload fail-open — constructing AuthService
    // and calling this method never touches AuthenticationService.Instance, so it
    // is exercisable in EditMode against a fake IBackendClient.
    public class AuthServiceTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public EmailAvailableResult CheckEmailAvailableResponse;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                object response = function switch
                {
                    "CheckEmailAvailable" => CheckEmailAvailableResponse,
                    _                     => null
                };
                return Task.FromResult((T)response);
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        [Test]
        public async Task IsEmailAvailable_fails_open_when_backend_returns_null()
        {
            var backend = new FakeBackendClient { CheckEmailAvailableResponse = null };
            var auth    = new AuthService(backend);

            // A null payload (e.g. a broken email_lookup index) must not block
            // registration — sign-up's ENTITY_EXISTS is the backstop.
            Assert.IsTrue(await auth.IsEmailAvailableAsync("someone@example.com"));
        }

        [Test]
        public async Task IsEmailAvailable_reflects_backend_result()
        {
            var backend = new FakeBackendClient
            {
                CheckEmailAvailableResponse = new EmailAvailableResult { Available = false }
            };
            var auth = new AuthService(backend);

            Assert.IsFalse(await auth.IsEmailAvailableAsync("taken@example.com"));
        }
    }
}
