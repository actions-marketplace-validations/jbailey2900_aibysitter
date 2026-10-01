using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aibysitter.Web.GitHub;

namespace Aibysitter.Rules.Tests.GitHub;

public class AppJwtTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + ((4 - s.Length % 4) % 4), '='));
    }

    [Fact]
    public void Create_ProducesVerifiableRs256Token_WithGitHubClaims()
    {
        using var rsa = RSA.Create(2048);

        var parts = AppJwt.Create("12345", rsa, Now).Split('.');

        Assert.Equal(3, parts.Length);
        using var header = JsonDocument.Parse(FromBase64Url(parts[0]));
        using var payload = JsonDocument.Parse(FromBase64Url(parts[1]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("12345", payload.RootElement.GetProperty("iss").GetString());
        Assert.Equal(Now.AddSeconds(-60).ToUnixTimeSeconds(), payload.RootElement.GetProperty("iat").GetInt64());
        Assert.Equal(Now.AddMinutes(9).ToUnixTimeSeconds(), payload.RootElement.GetProperty("exp").GetInt64());
        Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), FromBase64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.DoesNotContain('=', string.Concat(parts));
    }

    [Theory]
    [InlineData("5152568", "5152568")]
    [InlineData("Iv23liExampleClient", "Iv23liExampleClient")]
    [InlineData(" 5152568 ", "5152568")]
    public void Create_IssuerIsAppIdOrClientId_Trimmed(string issuer, string expected)
    {
        using var rsa = RSA.Create(2048);

        var payload = AppJwt.Create(issuer, rsa, Now).Split('.')[1];

        using var doc = JsonDocument.Parse(FromBase64Url(payload));
        Assert.Equal(expected, doc.RootElement.GetProperty("iss").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankIssuer_Throws(string issuer)
    {
        using var rsa = RSA.Create(2048);

        Assert.ThrowsAny<ArgumentException>(() => AppJwt.Create(issuer, rsa, Now));
    }

    [Fact]
    public void LoadPrivateKey_ReadsPkcs1Pem()
    {
        using var source = RSA.Create(2048);
        var path = Path.Combine(Path.GetTempPath(), $"aibysitter-test-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, source.ExportRSAPrivateKeyPem());
        try
        {
            using var loaded = AppJwt.LoadPrivateKey(path);
            var token = AppJwt.Create("1", loaded, Now).Split('.');

            Assert.True(source.VerifyData(Encoding.ASCII.GetBytes($"{token[0]}.{token[1]}"), FromBase64Url(token[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
