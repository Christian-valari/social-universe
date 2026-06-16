using UnityEngine;

namespace SocialUniverse.Config
{
    // How aggressively the chat moderation filter rewrites messages.
    // Off       — no client-side filtering (server may still enforce).
    // Moderate  — blocked words are masked, message still sends.
    // Strict    — a message containing a blocked word is rejected outright.
    public enum ChatFilterLevel
    {
        Off,
        Moderate,
        Strict
    }

    // M4 social tunables. The chat-filter defaults encode the provisional
    // teen-safe policy: Strict filtering for every player. The age policy /
    // content rating is still an Open Decision (see PROGRESS.md) — when it is
    // resolved, the per-age-band behavior lands here (and in AgeGateService,
    // M10) rather than in the social services themselves.
    [CreateAssetMenu(menuName = "SocialUniverse/Config/SocialConfig", fileName = "SocialConfig")]
    public class SocialConfig : ScriptableObject
    {
        [Header("Chat — Moderation (teen-safe defaults)")]
        [SerializeField] private ChatFilterLevel _chatFilterLevel = ChatFilterLevel.Strict;
        [SerializeField] private string[] _blockedWords =
        {
            // Designer-extendable starter list; must match BLOCKED_WORDS in
            // ServerCode/ModerateMessage.js. Matching is case-insensitive and
            // catches simple letter-for-symbol substitutions (a→@, i→1, …).
            "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
            "nigger", "nigga", "whore", "slut", "retard", "kys"
        };

        [Header("Chat — Channels")]
        [SerializeField] private string _globalChannelName  = "global";
        [SerializeField] private int    _maxMessageLength   = 200;
        [SerializeField] private int    _chatHistoryLimit   = 100;   // messages kept per channel client-side

        [Header("Presence & Shards")]
        [SerializeField] private int    _maxPlayersPerShard = 16;
        [SerializeField] private int    _maxShardsPerPlanet = 8;     // join attempts walk shard 0..N-1 before failing
        [SerializeField] private float  _positionSyncIntervalSec = 0.5f; // owner-side send rate for NetworkPlayer markers

        [Header("Profiles")]
        [SerializeField] private int    _maxDisplayNameLength = 20;

        public ChatFilterLevel ChatFilterLevel => _chatFilterLevel;
        public string[] BlockedWords           => _blockedWords;

        public string GlobalChannelName => _globalChannelName;
        public int    MaxMessageLength  => _maxMessageLength;
        public int    ChatHistoryLimit  => _chatHistoryLimit;

        public int   MaxPlayersPerShard      => _maxPlayersPerShard;
        public int   MaxShardsPerPlanet      => _maxShardsPerPlanet;
        public float PositionSyncIntervalSec => _positionSyncIntervalSec;

        public int MaxDisplayNameLength => _maxDisplayNameLength;
    }
}

