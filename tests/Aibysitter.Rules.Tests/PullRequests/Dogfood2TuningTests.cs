using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

/// <summary>False-positive shapes from dogfood 2, and the true positives next to them.</summary>
public class Dogfood2TuningTests
{
    private static List<PullRequestFinding> Run(IPullRequestCheck check, params ChangedFile[] files) => check.Evaluate(Context(files)).ToList();

    // P001

    [Theory]
    [InlineData("tests/Site/ContentPageTests.cs", "File.WriteAllText(path, \"---\\ntitle: Rules\\n---\\n::INTRO_TEXT::\\n\");")]
    [InlineData("tests/Reminders/StaffHomeTests.cs", "[\"Ops:Reminders:3:dueUtc\"] = \"::SOME_DATE::\",")]
    [InlineData("src/app/cart.test.ts", "const url = 'https://shop.example/?tag=::TAG::';")]
    public void P001_TestFiles_NotFlagged(string path, string line) =>
        Assert.Empty(Run(new PlaceholderIdentifiers(), Added(path, line)));

    [Fact]
    public void P001_DocumentedSqlTemplate_NotFlagged()
    {
        var file = Added("scripts/sql/backup-setup.sql",
            "/*",
            "    Run once as sysadmin after replacing every ::PLACEHOLDER::.",
            "",
            "    ::BACKUP_PATH::           Local backup root.",
            "    ::MASTER_KEY_PASSWORD::   Master key password.",
            "*/",
            "CREATE MASTER KEY ENCRYPTION BY PASSWORD = N'::MASTER_KEY_PASSWORD::';",
            "BACKUP CERTIFICATE C TO FILE = N'::BACKUP_PATH::\\cert\\c.cer';");

        Assert.Empty(Run(new PlaceholderIdentifiers(), file));
    }

    [Fact]
    public void P001_UndocumentedPlaceholderInTemplate_StillFlagged()
    {
        var file = Added("scripts/sql/backup-setup.sql",
            "/* Replace ::BACKUP_PATH:: before running. */",
            "BACKUP DATABASE Db TO DISK = N'::BACKUP_PATH::\\db.bak';",
            "CREATE LOGIN [::RUNNER_LOGIN::] FROM WINDOWS;");

        var finding = Assert.Single(Run(new PlaceholderIdentifiers(), file));
        Assert.Equal(3, finding.Line);
        Assert.Equal("Placeholder left in change: \"::RUNNER_LOGIN::\"", finding.Message);
    }

    [Fact]
    public void P001_MarkerNamedInDocComment_NotFlaggedInCode()
    {
        var file = Added("src/Affiliates/AffiliateOptions.cs",
            "/// <summary>Enabled programs may not carry ::PLACEHOLDER:: values.</summary>",
            "public sealed class AffiliateOptionsValidator",
            "{",
            "    void Check() => Fail(\"Enabled programs can't contain ::PLACEHOLDER:: values.\");",
            "}");

        Assert.Empty(Run(new PlaceholderIdentifiers(), file));
    }

    [Fact]
    public void P001_PythonDocstringInterior_NotFlagged()
    {
        var file = Added("scripts/ci_prepare_content.py",
            "\"\"\"CI quality gates: copies content to OUT_DIR.",
            "",
            "Placeholder markers (::NAME::) become \"CI\".",
            "\"\"\"",
            "import re");

        Assert.Empty(Run(new PlaceholderIdentifiers(), file));
    }

    [Fact]
    public void P001_BlockCommentInterior_FromHeadContent_NotFlagged()
    {
        var head = "/*\n   Replace ::ZONE_ID:: below.\n*/\nvar zone = \"::ZONE_ID::\";\nvar key = \"::API_KEY::\";";
        var file = new ChangedFile("src/Zone.cs", FileChangeStatus.Modified, "@@ -4,0 +4,2 @@\n+var zone = \"::ZONE_ID::\";\n+var key = \"::API_KEY::\";", HeadContent: head);

        var finding = Assert.Single(Run(new PlaceholderIdentifiers(), file));
        Assert.Equal(5, finding.Line);
    }

    [Theory]
    [InlineData("src/Web/appsettings.Production.json", "\"ZoneId\": \"::CLOUDFLARE_ZONE_ID::\"")]
    [InlineData("src/Web/appsettings.Production.json", "{ \"name\": \"Secret expires\", \"dueUtc\": \"::MS_SECRET_EXPIRY_UTC::\" }")]
    [InlineData("src/Billing.cs", "var apiKey = \"YOUR_API_KEY\";")]
    public void P001_PendingValues_StillFlagged(string path, string line) =>
        Assert.Single(Run(new PlaceholderIdentifiers(), Added(path, line)));

    // P003

    private const string HelperDelegation = """
        public class GuardTests
        {
            [Theory]
            [InlineData("omitted")]
            public Task Invite_needs_a_decision(string caseKey) =>
                RunAsync(caseKey);

            private async Task RunAsync(string caseKey)
            {
                var result = await SeedAsync(caseKey);
                Assert.Equal(caseKey, result);
            }
        }
        """;

    [Fact]
    public void P003_HelperThatAsserts_CountsAsAssertion() =>
        Assert.Empty(Run(new AssertNothingTests(), Added("tests/GuardTests.cs", HelperDelegation.Split('\n'))));

    [Fact]
    public void P003_HelperWithoutAssertion_StillFlagged()
    {
        var source = HelperDelegation.Replace("Assert.Equal(caseKey, result);", "_ = result;", StringComparison.Ordinal);

        var finding = Assert.Single(Run(new AssertNothingTests(), Added("tests/GuardTests.cs", source.Split('\n'))));
        Assert.Contains("Invite_needs_a_decision", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void P003_IsInfo() => Assert.Equal(Severity.Info, new AssertNothingTests().Severity);

    // P005

    [Theory]
    [InlineData("      ConnectionStrings__Db: Server=localhost,1433;Database=Ci;User Id=sa;Password=Ci_Only_Pa55word!;TrustServerCertificate=True")]
    [InlineData("      DB: Data Source=127.0.0.1;Password=Hunter2Prod;")]
    [InlineData("      DB: Server=(local);Password=Hunter2Prod;")]
    [InlineData("      DB: Server=.;Password=Hunter2Prod;")]
    public void P005_LocalConnectionStringInCi_NotFlagged(string line) =>
        Assert.Empty(Run(new SecretsInDiff(), Added(".github/workflows/ci.yml", line)));

    [Theory]
    [InlineData(".github/workflows/ci.yml", "      DB: Server=db.internal;Password=Hunter2Prod;")]
    [InlineData(".github/workflows/ci.yml", "      DB: Server=localhost.evil.example;Password=Hunter2Prod;")]
    [InlineData("deploy/appsettings.json", "\"Db\": \"Server=localhost;Password=Hunter2Prod;\"")]
    public void P005_RemoteOrNonCi_StillFlagged(string path, string line) =>
        Assert.Single(Run(new SecretsInDiff(), Added(path, line)));

    // P012

    [Theory]
    [InlineData("docs/brand/build.py", "print(\"built\", OUT)")]
    [InlineData("app/orders.py", "    print(order)")]
    public void P012_Print_NotFlagged(string path, string line) =>
        Assert.Empty(Run(new DebugLeftovers(), Added(path, line)));
}
