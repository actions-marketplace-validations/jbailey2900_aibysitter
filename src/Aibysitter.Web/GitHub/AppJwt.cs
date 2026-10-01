using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aibysitter.Web.GitHub;

/// <summary>GitHub App JWT (RS256). Valid from now-60s to now+9min (GitHub maximum is 10 min).</summary>
public static class AppJwt
{
    public static string Create(string issuer, RSA privateKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentNullException.ThrowIfNull(privateKey);

        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = issuer.Trim(),
        }));

        var signingInput = $"{header}.{payload}";
        var signature = privateKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    public static RSA LoadPrivateKey(string pemPath)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(pemPath));
        return rsa;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
