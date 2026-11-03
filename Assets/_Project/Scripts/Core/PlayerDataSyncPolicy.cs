using System;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    // Whether the game may go on only once the player's server data has loaded (PlayerDataPull), and
    // how to ask the player to retry when it hasn't. Required with the real backend; off where the
    // services are mocks that answer with nothing (dev mode, a scene opened standalone, PlayMode
    // tests), which keep going with each read's defaults. AskToRetry is supplied by the composition
    // root (UI.SyncRetryDialogView) because Core can't reference UI.
    public sealed class PlayerDataSyncPolicy
    {
        public bool Required { get; }

        // Shows why the game is waiting; completes when the player taps Retry.
        public Func<string, Task> AskToRetry { get; }

        public PlayerDataSyncPolicy(bool required, Func<string, Task> askToRetry = null)
        {
            Required   = required;
            AskToRetry = askToRetry ?? (_ => Task.CompletedTask);
        }
    }
}
