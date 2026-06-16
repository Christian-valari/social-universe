using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Economy;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class VisitorTrackerTests
    {
        private class FakeBackendClient : IBackendClient
        {
            public string CalledFunction;
            public Dictionary<string, object> CalledArgs;
            public RecordVisitResult RecordVisitResponse;

            public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
            {
                CalledFunction = function;
                CalledArgs     = args;

                if (function == "RecordVisit" && typeof(T) == typeof(RecordVisitResult))
                {
                    object response = RecordVisitResponse;
                    return Task.FromResult((T)response);
                }
                return Task.FromResult(default(T));
            }

            public Task CallAsync(string function, Dictionary<string, object> args = null) =>
                Task.CompletedTask;
        }

        [Test]
        public async Task RecordVisitAsync_calls_RecordVisit_with_tile_and_planet_ids()
        {
            var backend = new FakeBackendClient { RecordVisitResponse = new RecordVisitResult { Success = true, VisitCount = 3 } };
            var tracker = new VisitorTracker(backend);

            var result = await tracker.RecordVisitAsync("12", "Planet_Earth");

            Assert.AreEqual("RecordVisit", backend.CalledFunction);
            Assert.AreEqual("12", backend.CalledArgs["tileId"]);
            Assert.AreEqual("Planet_Earth", backend.CalledArgs["planetId"]);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.VisitCount);
        }
    }
}
