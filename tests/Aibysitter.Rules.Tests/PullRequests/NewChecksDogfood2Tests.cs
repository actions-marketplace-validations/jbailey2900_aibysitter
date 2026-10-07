using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

/// <summary>P015–P019: the dogfood 2 shapes each check targets, and lines next to them that are not findings.</summary>
public class NewChecksDogfood2Tests
{
    private static List<PullRequestFinding> Run(IPullRequestCheck check, params ChangedFile[] files) => check.Evaluate(Context(files)).ToList();

    private static ChangedFile Modified(string path, params string[] diff) =>
        new(path, FileChangeStatus.Modified, "@@ -10,4 +10,4 @@\n" + string.Join("\n", diff));

    // P015 UnpinnedActions

    [Theory]
    [InlineData(".github/workflows/ci.yml", "      - uses: actions/checkout@v5", "actions/checkout@v5")]
    [InlineData(".github/workflows/deploy.yml", "        uses: actions/download-artifact@v7", "actions/download-artifact@v7")]
    [InlineData(".github/workflows/ci.yml", "      - uses: \"jbailey2900/aibysitter@main\"", "jbailey2900/aibysitter@main")]
    [InlineData(".github/actions/setup/action.yml", "    - uses: actions/setup-node@3d3c42e", "actions/setup-node@3d3c42e")]
    [InlineData("action.yml", "    - uses: owner/repo/path@v1", "owner/repo/path@v1")]
    public void P015_Unpinned_Flagged(string path, string line, string reference) =>
        Assert.Equal($"Action not pinned to a commit SHA: {reference}", Assert.Single(Run(new UnpinnedActions(), Added(path, line))).Message);

    [Theory]
    [InlineData(".github/workflows/ci.yml", "      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1")]
    [InlineData(".github/workflows/ci.yml", "      - uses: ./")]
    [InlineData(".github/workflows/ci.yml", "      - uses: ./.github/actions/setup")]
    [InlineData(".github/workflows/ci.yml", "      - uses: docker://alpine:3.20")]
    [InlineData(".github/workflows/ci.yml", "      # - uses: actions/checkout@v5")]
    [InlineData("docs/ci.md", "- uses: actions/checkout@v5")]
    [InlineData(".gitlab-ci.yml", "  uses: actions/checkout@v5")]
    public void P015_PinnedLocalCommentedOrNotActions_NotFlagged(string path, string line) =>
        Assert.Empty(Run(new UnpinnedActions(), Added(path, line)));

    // P016 SecurityExemptions

    [Theory]
    [InlineData("src/Subscribers/SubscriberEndpoints.cs", "app.MapPost(WebhookPath, HandleWebhookAsync).DisableAntiforgery();")]
    [InlineData("src/Pages/Subscribe.cshtml.cs", "[IgnoreAntiforgeryToken]")]
    [InlineData("src/Pages/Status.cshtml.cs", "[IgnoreAntiforgeryToken(Order = 1001)]")]
    [InlineData("src/Api/HealthController.cs", "    [AllowAnonymous]")]
    [InlineData("src/Program.cs", "app.MapGet(\"/health\", () => \"ok\").AllowAnonymous();")]
    public void P016_Exemption_Flagged(string path, string line) =>
        Assert.Single(Run(new SecurityExemptions(), Added(path, line)));

    [Theory]
    [InlineData("tests/Site/FormAntiforgeryTests.cs", "[IgnoreAntiforgeryToken]")]
    [InlineData("src/Pages/Subscribe.cshtml.cs", "/// <summary>Forms that carry no token: /subscribe ([IgnoreAntiforgeryToken]).</summary>")]
    [InlineData("src/Docs.cs", "var text = \"Use [AllowAnonymous] sparingly\";")]
    [InlineData("docs/security.md", "[AllowAnonymous]")]
    [InlineData("src/Pages/Account.cshtml.cs", "[Authorize]")]
    public void P016_TestsCommentsStringsDocs_NotFlagged(string path, string line) =>
        Assert.Empty(Run(new SecurityExemptions(), Added(path, line)));

    // P017 LoosenedAssertions

    [Theory]
    [InlineData("-        Assert.Equal(\"no-store\", response.Headers.CacheControl?.ToString());", "+        Assert.True(response.Headers.CacheControl?.NoStore);", "Assert.Equal", "Assert.True")]
    [InlineData("-        Assert.Equal(1, Regex.Count(html, \"href=\\\"/d/x/\"));", "+        Assert.DoesNotContain(\"href=\\\"/d/x/\", html, StringComparison.Ordinal);", "Assert.Equal", "Assert.DoesNotContain")]
    [InlineData("-    expect(total).toBe(42);", "+    expect(total).toBeTruthy();", "toBe", "toBeTruthy")]
    public void P017_ExactReplacedByWeaker_Flagged(string removed, string added, string exact, string weaker)
    {
        var finding = Assert.Single(Run(new LoosenedAssertions(), Modified("tests/Site/CacheTests.cs", "     var response = await Get();", removed, added, "     }")));

        Assert.Equal($"Assertion loosened: {exact} replaced by {weaker}", finding.Message);
        Assert.Equal(11, finding.Line);
    }

    [Fact]
    public void P017_ExactKept_NotFlagged() =>
        Assert.Empty(Run(new LoosenedAssertions(), Modified("tests/Site/CacheTests.cs",
            "-        Assert.Equal(\"no-store\", cache);",
            "+        Assert.Equal(\"no-store, private\", cache);",
            "+        Assert.True(response.IsSuccessStatusCode);")));

    [Fact]
    public void P017_WeakerAddedWithoutExactRemoved_NotFlagged() =>
        Assert.Empty(Run(new LoosenedAssertions(), Modified("tests/Site/CacheTests.cs",
            "     Assert.Equal(200, status);",
            "+    Assert.Contains(\"ok\", body, StringComparison.Ordinal);")));

    [Fact]
    public void P017_DifferentHunks_NotPaired() =>
        Assert.Empty(Run(new LoosenedAssertions(), new ChangedFile("tests/Site/CacheTests.cs", FileChangeStatus.Modified,
            "@@ -10,1 +10,0 @@\n-        Assert.Equal(\"a\", x);\n@@ -40,0 +39,1 @@\n+        Assert.Contains(\"b\", y);")));

    [Fact]
    public void P017_NonTestOrAddedFile_NotChecked()
    {
        Assert.Empty(Run(new LoosenedAssertions(), Modified("src/Guard.cs", "-Assert.Equal(1, x);", "+Assert.True(x > 0);")));
        Assert.Empty(Run(new LoosenedAssertions(), Added("tests/NewTests.cs", "Assert.True(x > 0);")));
    }

    // P018 BrowserPolicyLoosened

    [Theory]
    [InlineData("src/Networking/SecurityHeaders.cs", "\"script-src 'self' 'unsafe-inline' https://www.googletagmanager.com\" +", "'unsafe-inline'")]
    [InlineData("src/Networking/SecurityHeaders.cs", "\"style-src 'self' 'unsafe-inline'\",", "'unsafe-inline'")]
    [InlineData("web.config", "<add name=\"Content-Security-Policy\" value=\"script-src 'self' 'unsafe-eval'\" />", "'unsafe-eval'")]
    [InlineData("src/Program.cs", "policy.AllowAnyOrigin().AllowAnyHeader();", "AllowAnyOrigin()")]
    [InlineData("src/Program.cs", "context.Response.Headers[\"Access-Control-Allow-Origin\"] = \"*\";", "Access-Control-Allow-Origin\"] = \"*")]
    public void P018_Loosened_Flagged(string path, string line, string quoted) =>
        Assert.Equal($"Browser security policy loosened: \"{quoted}\"", Assert.Single(Run(new BrowserPolicyLoosened(), Added(path, line))).Message);

    [Theory]
    [InlineData("src/Networking/SecurityHeaders.cs", "$\"script-src 'self' 'nonce-{nonce}'\",")]
    [InlineData("src/Networking/SecurityHeaders.cs", "// 'unsafe-inline' is not allowed here")]
    [InlineData("tests/Site/HeaderTests.cs", "Assert.DoesNotContain(\"'unsafe-inline'\", csp);")]
    [InlineData("src/Program.cs", "policy.WithOrigins(\"https://iquitcooking.com\");")]
    [InlineData("docs/csp.md", "Never use 'unsafe-inline'.")]
    public void P018_SafePolicyCommentsTestsDocs_NotFlagged(string path, string line) =>
        Assert.Empty(Run(new BrowserPolicyLoosened(), Added(path, line)));

    // P019 ConfigTodos

    [Theory]
    [InlineData("src/Web/appsettings.Production.json", "{ \"name\": \"DMCA agent renewal\", \"dueUtc\": \"2029-10-01\", \"note\": \"TBD: placeholder date.\" },", "TBD")]
    [InlineData("deploy/values.yml", "replicas: TODO", "TODO")]
    [InlineData("src/Api/Api.csproj", "<Description>FIXME</Description>", "FIXME")]
    [InlineData("deploy/settings.ini", "SupportEmail = TBD", "TBD")]
    public void P019_UnfinishedValue_Flagged(string path, string line, string quoted) =>
        Assert.Equal($"Unfinished value in config: \"{quoted}\"", Assert.Single(Run(new ConfigTodos(), Added(path, line))).Message);

    [Theory]
    [InlineData("deploy/values.yml", "# TODO: raise after load test")]
    [InlineData("src/Api/Api.csproj", "<!-- TODO: drop after .NET 11 -->")]
    [InlineData("src/Web/appsettings.json", "\"Label\": \"Todo list\"")]
    [InlineData("src/Orders.cs", "var status = \"TBD\";")]
    [InlineData("docs/launch.md", "- Support email: TBD")]
    public void P019_CommentsCodeDocsOrOtherWords_NotFlagged(string path, string line) =>
        Assert.Empty(Run(new ConfigTodos(), Added(path, line)));
}
