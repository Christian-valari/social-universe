using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using SocialUniverse.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace SocialUniverse.Tests
{
    // Fire-and-forget calls (`_ = SomethingAsync()`) drop any exception silently. Forget() logs it.
    public class TaskExtensionsTests
    {
        [Test]
        public void Forget_logs_a_task_that_already_failed()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[SU:Mining\] Starting idle mining failed: .*boom"));

            Task.FromException(new InvalidOperationException("boom")).Forget("Starting idle mining", SULog.Channel.Mining);
        }

        [Test]
        public async Task Forget_logs_a_task_that_fails_later()
        {
            var pending = new TaskCompletionSource<bool>();
            LogAssert.Expect(LogType.Error, new Regex(@"Claiming idle mining failed: .*late"));

            ((Task)pending.Task).Forget("Claiming idle mining", SULog.Channel.Mining);
            pending.SetException(new InvalidOperationException("late"));
            await Task.Delay(50);
        }

        [Test]
        public void Forget_is_silent_for_a_task_that_succeeds()
        {
            Task.CompletedTask.Forget("Nothing");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
