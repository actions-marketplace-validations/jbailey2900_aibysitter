using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

/// <summary>Token-shaped values are assembled at runtime so the repository holds no literal tokens.</summary>
public class SecretsInRulesFileTests
{
    private readonly SecretsInRulesFile _rule = new();

    private static string Body(int length) => string.Concat(Enumerable.Range(0, length).Select(i => "Q7r2M4k9Pl"[i % 10]));

    public static TheoryData<string, string> Secrets => new()
    {
        { "-----BEGIN " + "RSA PRIVATE KEY-----", "Private key" },
        { "AKIA" + "Q7R2M4K9PLD3WZTB", "AWS access key ID" },
        { "gh" + "p_" + Body(36), "GitHub token" },
        { "github_" + "pat_" + Body(30), "GitHub token" },
        { "xox" + "b-" + Body(24), "Slack token" },
        { "sk-" + "ant-" + Body(30), "Anthropic API key" },
        { "sk-" + "proj-" + Body(30), "OpenAI API key" },
        { "sk_" + "live_" + Body(24), "Stripe live key" },
        { "AI" + "za" + Body(35), "Google API key" },
        { "ey" + "J" + Body(12) + ".ey" + "J" + Body(12) + "." + Body(12), "JSON Web Token" },
        { "Server=db;User Id=app;Password=Hunter2Prod;", "Password in connection string" },
        { "postgres://app:Hunter2Prod@db.internal:5432/app", "Credentials in URL" },
    };

    [Theory]
    [MemberData(nameof(Secrets))]
    public void Detects(string value, string kind)
    {
        var finding = Assert.Single(_rule.Evaluate(RulesFile.Parse($"- Use {value} for access.")));

        Assert.Contains(kind, finding.Message);
    }

    [Theory]
    [MemberData(nameof(Secrets))]
    public void Message_DoesNotContainSecret(string value, string kind)
    {
        _ = kind;
        var finding = Assert.Single(_rule.Evaluate(RulesFile.Parse(value)));

        Assert.DoesNotContain(value, finding.Message);
        Assert.DoesNotContain(value[^6..], finding.Message);
    }

    [Fact]
    public void CodeBlock_Included()
    {
        Assert.Single(_rule.Evaluate(RulesFile.Parse("```\nPassword=Hunter2Prod\n```\n")));
    }

    [Theory]
    [InlineData("Password=<your-password>")]
    [InlineData("Password=${DB_PASSWORD}")]
    [InlineData("Password=****")]
    [InlineData("Password=changeme")]
    [InlineData("Password=your_password_here")]
    [InlineData("Password=`abcd`")]
    [InlineData("postgres://app:app@localhost/app_test")]
    [InlineData("mysql://root:Hunter2Prod@127.0.0.1:3306/app")]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    [InlineData("Set the password in the PASSWORD environment variable.")]
    [InlineData("redis://default:abc123@cache.internal:6379")]
    [InlineData("rediss://user:pass@host:6380")]
    [InlineData("Password=qwerty")]
    public void Placeholders_AndLocalhost_Ignored(string line)
    {
        Assert.Empty(_rule.Evaluate(RulesFile.Parse(line)));
    }

    [Fact]
    public void ExampleCredentials_Fixture_OnlyRealValueFlagged()
    {
        var finding = Assert.Single(_rule.Evaluate(Fixtures.Load("v3/R009-example-credentials.md")));

        Assert.Equal(5, finding.Line);
    }

    [Fact]
    public void CommonValue_InsideLongerValue_StillFlagged()
    {
        Assert.Single(_rule.Evaluate(RulesFile.Parse("Password=Pass2024!x")));
    }
}
