using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SocialUniverse.Core;
using SocialUniverse.Social;

namespace SocialUniverse.Tests
{
    // Shared test doubles for the M4 Social suite. Internal to the test
    // assembly — one file to avoid each test re-declaring the same fakes
    // (the per-file FakeBackendClient precedent in Economy tests predates
    // the Social suite needing two fakes at once).
    internal class FakeChatService : IChatService
    {
        public readonly List<(string Channel, string Text)> SentMessages  = new();
        public readonly List<(string PlayerId, string Text)> SentDirects  = new();
        public readonly List<string> JoinedChannels = new();
        public readonly List<string> LeftChannels   = new();
        public readonly List<string> BlockedPlayers = new();

        public event Action<ChatMessage> MessageReceived;
        public event Action<ChatMessage> DirectMessageReceived;

        public bool IsConnected { get; private set; }

        public Task ConnectAsync(string displayName, string avatarId) { IsConnected = true;  return Task.CompletedTask; }
        public Task DisconnectAsync()                { IsConnected = false; return Task.CompletedTask; }

        public Task JoinChannelAsync(string channelName)  { JoinedChannels.Add(channelName); return Task.CompletedTask; }
        public Task LeaveChannelAsync(string channelName) { LeftChannels.Add(channelName);   return Task.CompletedTask; }

        public Task SendMessageAsync(string channelName, string text)
        {
            SentMessages.Add((channelName, text));
            return Task.CompletedTask;
        }

        public Task SendDirectMessageAsync(string playerId, string text)
        {
            SentDirects.Add((playerId, text));
            return Task.CompletedTask;
        }

        public Task BlockPlayerAsync(string playerId)   { BlockedPlayers.Add(playerId);    return Task.CompletedTask; }
        public Task UnblockPlayerAsync(string playerId) { BlockedPlayers.Remove(playerId); return Task.CompletedTask; }

        public void RaiseChannelMessage(ChatMessage message) => MessageReceived?.Invoke(message);
        public void RaiseDirectMessage(ChatMessage message)  => DirectMessageReceived?.Invoke(message);
    }

    internal class FakeSocialBackendClient : IBackendClient
    {
        public string CalledFunction;
        public Dictionary<string, object> CalledArgs;
        public readonly List<string> AllCalledFunctions = new();

        public ReportResult        ReportResponse;
        public BlockResult         BlockResponse;
        public PlayerProfile       ProfileResponse;
        public ProfileUpdateResult ProfileUpdateResponse;

        public Task<T> CallAsync<T>(string function, Dictionary<string, object> args = null)
        {
            CalledFunction = function;
            CalledArgs     = args;
            AllCalledFunctions.Add(function);

            object response = function switch
            {
                "SubmitReport"     => ReportResponse,
                "BlockUser"        => BlockResponse,
                "GetPlayerProfile" => ProfileResponse,
                "UpdateProfile"    => ProfileUpdateResponse,
                _                  => null
            };
            return Task.FromResult(response is T typed ? typed : default);
        }

        public Task CallAsync(string function, Dictionary<string, object> args = null)
        {
            CalledFunction = function;
            CalledArgs     = args;
            AllCalledFunctions.Add(function);
            return Task.CompletedTask;
        }
    }
}
