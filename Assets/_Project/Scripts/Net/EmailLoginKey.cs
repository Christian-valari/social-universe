using System.Security.Cryptography;
using System.Text;

namespace SocialUniverse.Net
{
    // UGS's username/password auth only supports sign-in by username (3-20 chars,
    // alphanumeric + ".-@_") and real emails often exceed that length. This derives
    // a deterministic, UGS-valid login key from the email so login needs no server
    // round-trip to resolve it, and UGS's own uniqueness check on the key doubles
    // as "email already registered" enforcement at signup.
    public static class EmailLoginKey
    {
        public static string Derive(string email)
        {
            string normalized = email.Trim().ToLowerInvariant();
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var sb = new StringBuilder(40);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString(0, 20);
        }
    }
}
