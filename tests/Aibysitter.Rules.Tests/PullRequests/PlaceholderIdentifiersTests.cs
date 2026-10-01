using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class PlaceholderIdentifiersTests
{
    private readonly PlaceholderIdentifiers check = new();

    [Fact]
    public void PatchFixture_FlagsAddedLinesOnly_WithHeadLineNumbers()
    {
        var findings = check.Evaluate(Context(FromPatchFixture("src/OrderService.cs", "placeholders.patch"))).ToList();

        Assert.Equal(3, findings.Count);
        Assert.Equal(new[] { 12, 13, 14 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("src/OrderService.cs", f.Path));
        Assert.Contains("\"YOUR_API_KEY\"", findings[0].Message);
        Assert.Contains("\"::CURRENT_USER_ID::\"", findings[1].Message);
    }

    [Theory]
    [InlineData("var x = ::USER_ID::;")]
    [InlineData("var k = \"REPLACE_ME\";")]
    [InlineData("\"ApiKey\": \"YOUR_TOKEN_HERE\"")]
    [InlineData("<add key=\"url\" value=\"<your-endpoint>\" />")]
    [InlineData("var p = \"<Placeholder>\";")]
    [InlineData("Call(TODO_IMPLEMENT);")]
    public void Flags(string line)
    {
        Assert.Single(check.Evaluate(Context(Added("src/A.cs", line))));
    }

    [Theory]
    [InlineData("auto n = std::string::npos;")]
    [InlineData("var yourApiKey = config[\"ApiKey\"];")]
    [InlineData("// TODO: tidy")]
    [InlineData("var x = global::System.String.Empty;")]
    public void DoesNotFlag(string line)
    {
        Assert.Empty(check.Evaluate(Context(Added("src/A.cs", line))));
    }

    [Theory]
    [InlineData("appsettings.json", true)]
    [InlineData("deploy/values.yaml", true)]
    [InlineData("src/App.csproj", true)]
    [InlineData("README.md", false)]
    [InlineData("notes.txt", false)]
    public void FileKinds_CodeAndConfigOnly(string path, bool flagged)
    {
        Assert.Equal(flagged, check.Evaluate(Context(Added(path, "key = YOUR_API_KEY"))).Any());
    }

    [Fact]
    public void RemovedFiles_AreSkipped()
    {
        var file = new ChangedFile("src/A.cs", FileChangeStatus.Removed, "@@ -1,1 +0,0 @@\n-var x = ::USER_ID::;");

        Assert.Empty(check.Evaluate(Context(file)));
    }
}
