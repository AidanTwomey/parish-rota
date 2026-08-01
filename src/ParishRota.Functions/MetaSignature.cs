using System.Security.Cryptography;
using System.Text;

namespace ParishRota.Functions;

/// <summary>
/// Verifies the <c>X-Hub-Signature-256</c> header Meta sends with every webhook
/// POST: an HMAC-SHA256 of the request body, keyed on the Meta app secret,
/// formatted as <c>sha256=</c> followed by 64 lowercase hex characters.
///
/// This is the only thing standing between the public internet and a Reader's
/// Slot — the endpoint has to be anonymous because Meta cannot present a
/// Functions key (ADR 0001).
/// </summary>
public static class MetaSignature
{
    private const string Prefix = "sha256=";
    private const int DigestBytes = 32;

    /// <summary>
    /// True only if <paramref name="header"/> is a well-formed signature over
    /// exactly these bytes under this secret. Every other case — no secret, no
    /// header, wrong shape, wrong digest — is false: it fails closed.
    /// </summary>
    /// <param name="body">
    /// The raw request bytes, exactly as received. Re-serialising the JSON first
    /// would change the whitespace and the signature would never match.
    /// </param>
    public static bool IsValid(ReadOnlySpan<byte> body, string? header, string? appSecret)
    {
        if (string.IsNullOrEmpty(appSecret) || header is null)
        {
            return false;
        }

        if (!header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presentedHex = header.AsSpan(Prefix.Length);
        if (presentedHex.Length != DigestBytes * 2)
        {
            return false;
        }

        Span<byte> presented = stackalloc byte[DigestBytes];
        if (!Convert.TryFromHexString(presentedHex, presented, out var written) || written != DigestBytes)
        {
            return false;
        }

        Span<byte> computed = stackalloc byte[DigestBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body, computed);

        // Fixed-time rather than SequenceEqual: a short-circuiting comparison
        // leaks, byte by byte, how much of a guessed signature was correct.
        return CryptographicOperations.FixedTimeEquals(computed, presented);
    }
}
