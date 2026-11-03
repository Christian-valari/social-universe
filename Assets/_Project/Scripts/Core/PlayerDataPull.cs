using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    // Loads the player's server data for a scene before it opens
    // (docs/superpowers/plans/2026-10-18-cloud-synced-planet-data.md). Every step runs at once. When
    // `required` (the real backend), a step that fails asks the player to retry and only the failed
    // steps run again, until every one has loaded — so a scene never opens with defaults standing in
    // for the player's data. Otherwise (dev-mode, standalone and test scopes, whose mocks answer with
    // nothing) a failed step is logged and skipped, leaving that step's defaults.
    public static class PlayerDataPull
    {
        public const string RetryMessage = "Couldn't load your game data.\nCheck your connection and try again.";

        // onProgress gets the share of steps loaded so far (0..1), for the loading bar.
        public static async Task RunAsync(IReadOnlyList<(string Name, Func<Task> Run)> steps, bool required,
            Func<string, Task> askToRetry, Action<float> onProgress = null)
        {
            var gate    = new object();
            int total   = steps.Count;
            int loaded  = 0;
            var pending = steps.ToList();

            while (pending.Count > 0)
            {
                var failed = new List<(string Name, Func<Task> Run)>();
                await Task.WhenAll(pending.Select(async step =>
                {
                    try
                    {
                        await step.Run();
                        float progress;
                        lock (gate) progress = (float)++loaded / total;
                        onProgress?.Invoke(progress);
                    }
                    catch (Exception ex)
                    {
                        SULog.Warn($"PlayerDataPull: {step.Name} failed to load ({ex.Message})", SULog.Channel.Net);
                        lock (gate) failed.Add(step);
                    }
                }));

                if (failed.Count == 0) return;
                if (!required)
                {
                    onProgress?.Invoke(1f);
                    return;
                }

                await askToRetry(RetryMessage);
                pending = failed;
            }
        }
    }
}
