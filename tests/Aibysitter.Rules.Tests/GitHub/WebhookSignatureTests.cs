using System.Text;
using Aibysitter.Web.GitHub;

namespace Aibysitter.Rules.Tests.GitHub;

public class WebhookSignatureTests
{
    private const string Secret = "It's a Secret to Everybody";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("Hello, World!");

    [Fact]
    public void Compute_MatchesGitHubDocumentedExample()
    {
        // Example from GitHub's "Validating webhook deliveries" documentation.
        Assert.Equal(
            "sha256=757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17",
            WebhookSignature.Compute(Secret, Body));
    }

    [Fact]
    public void IsValid_AcceptsCorrectSignature_AnyHexCase()
    {
        var header = WebhookSignature.Compute(Secret, Body);

        Assert.True(WebhookSignature.IsValid(Secret, Body, header));
        Assert.True(WebhookSignature.IsValid(Secret, Body, "sha256=" + header["sha256=".Length..].ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1=757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17")]
    [InlineData("757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17")]
    [InlineData("sha256=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("sha256=757107ea")]
    public void IsValid_RejectsBadHeaders(string? header)
    {
        Assert.False(WebhookSignature.IsValid(Secret, Body, header));
    }

    [Fact]
    public void IsValid_RejectsWrongSecret_AndTamperedBody()
    {
        var header = WebhookSignature.Compute(Secret, Body);

        Assert.False(WebhookSignature.IsValid("other", Body, header));
        Assert.False(WebhookSignature.IsValid(Secret, Encoding.UTF8.GetBytes("Hello, World?"), header));
    }

    [Fact]
    public void IsValid_RejectsEmptySecret()
    {
        Assert.False(WebhookSignature.IsValid("", Body, WebhookSignature.Compute("", Body)));
    }
}
