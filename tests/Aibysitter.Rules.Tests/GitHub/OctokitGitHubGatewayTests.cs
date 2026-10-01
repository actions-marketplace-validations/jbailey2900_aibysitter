using System.Security.Cryptography;
using Aibysitter.Web.GitHub;
using Microsoft.Extensions.Options;

namespace Aibysitter.Rules.Tests.GitHub;

public class OctokitGitHubGatewayTests
{
    [Fact]
    public void PrivateKey_LoadFailure_IsNotCached()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aibysitter-key-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, string.Empty);
        try
        {
            using var gateway = new OctokitGitHubGateway(
                Options.Create(new GitHubOptions { AppId = "1", WebhookSecret = "s", PrivateKeyPath = path }),
                TimeProvider.System);

            Assert.ThrowsAny<ArgumentException>(() => gateway.PrivateKey);

            using var rsa = RSA.Create(2048);
            File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem());

            Assert.Equal(rsa.ExportParameters(false).Modulus, gateway.PrivateKey.ExportParameters(false).Modulus);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
