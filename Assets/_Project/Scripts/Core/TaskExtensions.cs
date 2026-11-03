using System;
using System.Threading.Tasks;

namespace SocialUniverse.Core
{
    public static class TaskExtensions
    {
        // For fire-and-forget calls: `_ = SomethingAsync()` drops an exception silently, while
        // `SomethingAsync().Forget("Doing something")` logs it as "Doing something failed: ...".
        public static async void Forget(this Task task, string what, SULog.Channel channel = SULog.Channel.Core)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                SULog.Error($"{what} failed: {ex}", channel);
            }
        }
    }
}
