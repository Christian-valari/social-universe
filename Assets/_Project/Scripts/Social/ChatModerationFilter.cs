using System;
using SocialUniverse.Config;

namespace SocialUniverse.Social
{
    // What the filter decided about an outbound message.
    public enum ChatFilterVerdict
    {
        Allowed,   // passed unchanged
        Masked,    // blocked words replaced with '*' (Moderate level)
        Rejected   // message must not be sent (Strict level)
    }

    // Client-side profanity filter (the server also enforces — see
    // ServerCode/ModerateMessage.js, whose word list must match
    // SocialConfig.BlockedWords). Pure C#, no Unity dependency beyond the
    // config SO, so it is fully EditMode-testable.
    //
    // Matching is case-insensitive and normalizes common letter-for-symbol
    // substitutions (a→@, i→1, o→0, …). Normalization is strictly
    // char-for-char, so indices in the normalized text map 1:1 back to the
    // original — that's what lets Sanitize mask in place.
    public class ChatModerationFilter
    {
        private readonly SocialConfig _config;
        private readonly string[]     _blockedWords;

        public ChatModerationFilter(SocialConfig config)
        {
            _config       = config;
            _blockedWords = new string[config.BlockedWords.Length];
            for (int i = 0; i < _blockedWords.Length; i++)
                _blockedWords[i] = Normalize(config.BlockedWords[i]);
        }

        // True if the text contains no blocked words after normalization.
        public bool IsClean(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            var normalized = Normalize(text);
            foreach (var word in _blockedWords)
                if (normalized.Contains(word)) return false;
            return true;
        }

        // Replaces every blocked-word occurrence with '*'s, preserving length.
        public string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var normalized = Normalize(text);
            var chars      = text.ToCharArray();

            foreach (var word in _blockedWords)
            {
                int index = 0;
                while ((index = normalized.IndexOf(word, index, StringComparison.Ordinal)) >= 0)
                {
                    for (int i = index; i < index + word.Length; i++)
                        chars[i] = '*';
                    index += word.Length;
                }
            }
            return new string(chars);
        }

        // Applies SocialConfig.ChatFilterLevel to an outbound message.
        // On Rejected, `output` is null and the message must not be sent.
        public ChatFilterVerdict Apply(string text, out string output)
        {
            switch (_config.ChatFilterLevel)
            {
                case ChatFilterLevel.Off:
                    output = text;
                    return ChatFilterVerdict.Allowed;

                case ChatFilterLevel.Moderate:
                    output = Sanitize(text);
                    return output == text ? ChatFilterVerdict.Allowed : ChatFilterVerdict.Masked;

                default: // Strict
                    if (IsClean(text))
                    {
                        output = text;
                        return ChatFilterVerdict.Allowed;
                    }
                    output = null;
                    return ChatFilterVerdict.Rejected;
            }
        }

        // Safety net for inbound messages: we can't reject another player's
        // message client-side, so anything above Off gets masked for display.
        public string SanitizeIncoming(string text) =>
            _config.ChatFilterLevel == ChatFilterLevel.Off ? text : Sanitize(text);

        private static string Normalize(string text)
        {
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                chars[i] = NormalizeChar(chars[i]);
            return new string(chars);
        }

        private static char NormalizeChar(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case '@': case '4': return 'a';
                case '1': case '!': return 'i';
                case '0':           return 'o';
                case '3':           return 'e';
                case '$': case '5': return 's';
                case '7':           return 't';
                default:            return char.ToLowerInvariant(c);
            }
        }
    }
}
