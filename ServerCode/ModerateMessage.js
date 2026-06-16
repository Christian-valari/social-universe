// ModerateMessage — server-side text moderation. Returns whether the text is
// allowed and a masked version. Used by UpdateProfile for display names, and
// available to any future server-mediated message path. (Vivox channel chat is
// filtered client-side by ChatModerationFilter and by Vivox's own moderation
// tooling — this function is the in-house enforcement point.)
// BLOCKED_WORDS and the normalization map must match SocialConfig.BlockedWords
// / ChatModerationFilter.NormalizeChar — same "must match" pattern as
// ClaimYield.js's yield constants.

const BLOCKED_WORDS = [
  "fuck", "shit", "bitch", "asshole", "cunt", "dick", "faggot",
  "nigger", "nigga", "whore", "slut", "retard", "kys"
];

const MAX_MESSAGE_LENGTH = 200; // must match SocialConfig.MaxMessageLength

const CHAR_MAP = { "@": "a", "4": "a", "1": "i", "!": "i", "0": "o", "3": "e", "$": "s", "5": "s", "7": "t" };

function normalize(text) {
  let out = "";
  for (const ch of text.toLowerCase()) out += CHAR_MAP[ch] ?? ch;
  return out;
}

function moderate(text) {
  const normalized = normalize(text);
  let masked = text;
  let clean  = true;

  for (const word of BLOCKED_WORDS) {
    let index = 0;
    while ((index = normalized.indexOf(word, index)) >= 0) {
      clean  = false;
      masked = masked.slice(0, index) + "*".repeat(word.length) + masked.slice(index + word.length);
      index += word.length;
    }
  }
  return { clean, masked };
}

/**
 * @param {string} text - The text to moderate. Must be non-empty and at most 200 characters.
 */
module.exports = async ({ params, context, logger }) => {
  const { text } = params;

  if (typeof text !== "string" || text.length === 0 || text.length > MAX_MESSAGE_LENGTH) {
    throw new Error("Invalid params: text is required and must be at most 200 characters");
  }

  const { clean, masked } = moderate(text);

  if (!clean) {
    logger.info(`ModerateMessage: blocked content from ${context.playerId}`);
  }
  return { allowed: clean, filteredText: masked };
};
