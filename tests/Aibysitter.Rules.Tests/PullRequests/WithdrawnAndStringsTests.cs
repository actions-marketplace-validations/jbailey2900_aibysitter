using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class WithdrawnAndStringsTests
{
    [Fact]
    public void ReservedIds_NotUsedByAnyCheckOrDoc()
    {
        Assert.Equal(["P017"], PullRequestCheckDocs.Reserved);
        Assert.DoesNotContain(PullRequestReviewer.DiscoverChecks(), c => PullRequestCheckDocs.Reserved.Contains(c.Id));
        Assert.DoesNotContain(PullRequestCheckDocs.All, d => PullRequestCheckDocs.Reserved.Contains(d.Id));
    }

    [Fact]
    public void P017_Withdrawal_InChangelog() =>
        Assert.Contains(Aibysitter.Rules.RulesetChangelog.AppChecks, e => e.Title == "P017 LoosenedAssertions withdrawn" && e.Changes.Single().Contains("per-commit review", StringComparison.Ordinal));

    [Theory]
    [InlineData("tests/ConfigTodosTests.cs", "    [InlineData(\"deploy/values.yml\", \"# TODO: raise after load test\")]")]
    [InlineData("tests/ConfigTodosTests.cs", "    [InlineData(\"src/Api/Api.csproj\", \"-- TODO drop after .NET 11\")]")]
    [InlineData("src/Docs.cs", "var hint = \"Avoid // TODO comments\";")]
    [InlineData("src/Docs.cs", "var example = \"throw new NotImplementedException();\";")]
    [InlineData("scripts/check.py", "PATTERN = '# TODO'")]
    public void P002_InsideStringLiteral_NotFlagged(string path, string line) =>
        Assert.Empty(new TodoStubs().Evaluate(Context(Added(path, line))));

    [Theory]
    [InlineData("src/Orders.cs", "var name = \"orders\"; // TODO rename")]
    [InlineData("src/Orders.cs", "public decimal Total() => throw new NotImplementedException();")]
    [InlineData("scripts/check.py", "x = 'a'  # TODO handle b")]
    public void P002_OutsideStringLiteral_StillFlagged(string path, string line) =>
        Assert.Single(new TodoStubs().Evaluate(Context(Added(path, line))));
}
