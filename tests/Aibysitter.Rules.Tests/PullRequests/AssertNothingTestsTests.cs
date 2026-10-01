using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class AssertNothingTestsTests
{
    private const string TestPath = "tests/Demo.Tests/CalculatorTests.cs";

    private readonly AssertNothingTests check = new();

    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "pr", "CalculatorTests.cs.txt"));

    private static ChangedFile Modified(params int[] addedLines)
    {
        var content = Fixture().Replace("\r\n", "\n").Split('\n');
        var hunks = string.Join("\n", addedLines.Select(n => $"@@ -{n},0 +{n},1 @@\n+{content[n - 1]}"));
        return new ChangedFile(TestPath, FileChangeStatus.Modified, hunks, HeadContent: Fixture());
    }

    [Fact]
    public void AddedFile_FlagsOnlyAssertNothingTests()
    {
        var file = new ChangedFile(TestPath, FileChangeStatus.Added, Added(TestPath, Fixture().Split('\n')).Patch, HeadContent: Fixture());

        var findings = check.Evaluate(Context(file)).ToList();

        Assert.Equal(new[] { 15, 23, 33, 53, 59 }, findings.Select(f => f.Line));
        Assert.Equal(
            new[] { "Add_DoesNotThrow", "Square_Runs", "Empty", "BraceInString_NoAssert", "AsyncGeneric_NoAssert" },
            findings.Select(f => f.Message.Split('"')[1]));
    }

    [Fact]
    public void ModifiedFile_ChangeInsideAssertingTest_NoFinding()
    {
        Assert.Empty(check.Evaluate(Context(Modified(10))));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(21)]
    [InlineData(20)]
    public void ModifiedFile_ChangeInsideAssertNothingTest_FlagsIt(int addedLine)
    {
        var finding = Assert.Single(check.Evaluate(Context(Modified(addedLine))));

        Assert.Equal(23, finding.Line);
        Assert.Equal("P003", finding.CheckId);
    }

    [Fact]
    public void AddedFile_WithoutHeadContent_UsesPatch()
    {
        var file = Added(TestPath, "public class T", "{", "    [Fact]", "    public void Nothing()", "    {", "    }", "}") with { HeadContent = null };

        Assert.Equal(4, Assert.Single(check.Evaluate(Context(file))).Line);
    }

    [Fact]
    public void ModifiedFile_WithoutHeadContent_IsSkipped()
    {
        Assert.Empty(check.Evaluate(Context(Modified(25) with { HeadContent = null })));
    }

    [Theory]
    [InlineData("[Test]")]
    [InlineData("[TestCase(1)]")]
    [InlineData("[TestMethod]")]
    [InlineData("[DataTestMethod]")]
    [InlineData("[Fact, Trait(\"k\", \"v\")]")]
    public void OtherFrameworkAttributes_AreDetected(string attribute)
    {
        var file = Added("tests/X.cs", attribute, "public void Nothing() { var x = 1; }");

        Assert.Single(check.Evaluate(Context(file)));
    }

    [Fact]
    public void NonCSharpFiles_AreIgnored()
    {
        Assert.Empty(check.Evaluate(Context(Added("tests/x.py", "[Fact]", "def test_nothing(): pass"))));
    }
}
