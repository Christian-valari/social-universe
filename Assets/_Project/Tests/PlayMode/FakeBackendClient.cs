using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // Scripted IBackendClient for PlayMode tests. Each Cloud Code function can be given a
    // responder that returns the JSON the real function would; the JSON is deserialized into the
    // caller's response type with JsonUtility, so field names must match the C# DTO (case-sensitive).
    // Unscripted functions return default(T), the same as LocalMockBackendClient.
    public class FakeBackendClient : IBackendClient
    {
        private readonly Dictionary<string, Func<Dictionary<string, object>, string>> _responders = new();

        public readonly List<(string Function, Dictionary<string, object> Args)> Calls = new();

        public void On(string function, Func<Dictionary<string, object>, string> jsonResponder) =>
            _responders[function] = jsonResponder;

        public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
        {
            Calls.Add((function, args));
            return Task.FromResult(_responders.TryGetValue(function, out var respond)
                ? JsonUtility.FromJson<T>(respond(args))
                : default);
        }

        public Task CallAsync(string function, Dictionary<string, object> args = null)
        {
            Calls.Add((function, args));
            return Task.CompletedTask;
        }
    }
}
