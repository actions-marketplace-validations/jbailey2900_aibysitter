using System.Security.Cryptography;
using System.Text;

namespace Aibysitter.Web.GitHub;

public static class WebhookSignature
{
    public const string HeaderName = "X-Hub-Signature-256";
    private const string Prefix = "sha256=";

    public static string Compute(string secret, ReadOnlySpan<byte> body) =>
        Prefix + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    public static bool IsValid(string secret, ReadOnlySpan<byte> body, string? header)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header) || !header.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Compute(secret, body));
        var actual = Encoding.ASCII.GetBytes(header.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
