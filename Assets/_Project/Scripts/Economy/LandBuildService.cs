using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;

namespace SocialUniverse.Economy
{
    // Result shapes are public so tests can construct them for a fake IBackendClient.
    public class PlaceBuildResult  { public bool Success; public string Reason; public int NewBalance = -1; public int BuildLevel = -1; }
    public class RemoveBuildResult { public bool Success; public string Reason; public int BuildLevel = -1; }
    public class MoveBuildResult   { public bool Success; public string Reason; }
    public class PurchaseHexatileResult { public bool Success; public string Reason; public int NewBalance = -1; public int UnlockedCount = -1; }

    // Client wrapper over the PlaceBuild/RemoveBuild/MoveBuild cloud functions.
    // Pure request/response: performs no local state mutation. Callers apply the
    // resulting slot change to the LandBuildingHandoff (see LandBuildingController).
    public class LandBuildService
    {
        private readonly IBackendClient _backend;

        public LandBuildService(IBackendClient backend) => _backend = backend;

        public async Task<PlaceBuildResult> PlaceAsync(string tileId, string planetId, int hexIndex, string itemId, int cost)
        {
            try
            {
                var res = await _backend.CallAsync<PlaceBuildResult>("PlaceBuild",
                    new Dictionary<string, object>
                    {
                        { "tileId",   tileId   },
                        { "planetId", planetId },
                        { "hexIndex", hexIndex },
                        { "itemId",   itemId   },
                        { "cost",     cost     },
                    });
                return res ?? new PlaceBuildResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.Place failed — {ex.Message}", SULog.Channel.Economy);
                return new PlaceBuildResult { Success = false, Reason = "Network error" };
            }
        }

        public async Task<RemoveBuildResult> RemoveAsync(string tileId, string planetId, int hexIndex)
        {
            try
            {
                var res = await _backend.CallAsync<RemoveBuildResult>("RemoveBuild",
                    new Dictionary<string, object>
                    {
                        { "tileId",   tileId   },
                        { "planetId", planetId },
                        { "hexIndex", hexIndex },
                    });
                return res ?? new RemoveBuildResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.Remove failed — {ex.Message}", SULog.Channel.Economy);
                return new RemoveBuildResult { Success = false, Reason = "Network error" };
            }
        }

        public async Task<MoveBuildResult> MoveAsync(string tileId, string planetId, int fromHex, int toHex)
        {
            try
            {
                var res = await _backend.CallAsync<MoveBuildResult>("MoveBuild",
                    new Dictionary<string, object>
                    {
                        { "tileId",   tileId   },
                        { "planetId", planetId },
                        { "fromHex",  fromHex  },
                        { "toHex",    toHex    },
                    });
                return res ?? new MoveBuildResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.Move failed — {ex.Message}", SULog.Channel.Economy);
                return new MoveBuildResult { Success = false, Reason = "Network error" };
            }
        }

        public async Task<PurchaseHexatileResult> PurchaseHexatileAsync(string tileId, string planetId, int hexIndex)
        {
            try
            {
                var res = await _backend.CallAsync<PurchaseHexatileResult>("PurchaseHexatile",
                    new Dictionary<string, object>
                    {
                        { "tileId",   tileId   },
                        { "planetId", planetId },
                        { "hexIndex", hexIndex },
                    });
                return res ?? new PurchaseHexatileResult { Success = false, Reason = "No response" };
            }
            catch (Exception ex)
            {
                SULog.Error($"LandBuildService.PurchaseHexatile failed — {ex.Message}", SULog.Channel.Economy);
                return new PurchaseHexatileResult { Success = false, Reason = "Network error" };
            }
        }
    }
}
