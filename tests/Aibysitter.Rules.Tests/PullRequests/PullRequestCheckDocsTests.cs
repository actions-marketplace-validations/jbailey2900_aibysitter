using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class PullRequestCheckDocsTests
{
    [Fact]
    public void EveryCheck_HasDoc_AndEveryDoc_HasCheck()
    {
        Assert.Equal(
            PullRequestReviewer.DiscoverChecks().Select(c => c.Id),
            PullRequestCheckDocs.All.Select(d => d.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void DocName_MatchesCheckClassName()
    {
        Assert.All(PullRequestReviewer.DiscoverChecks(), c => Assert.Equal(c.GetType().Name, PullRequestCheckDocs.Find(c.Id)!.Name));
    }

    [Theory]
    [InlineData("P001")]
    [InlineData("P002")]
    [InlineData("P003")]
    [InlineData("P005")]
    [InlineData("P006")]
    [InlineData("P007")]
    [InlineData("P009")]
    [InlineData("P012")]
    public void CodeExamples_BehaveAsDocumented(string id)
    {
        var doc = PullRequestCheckDocs.Find(id)!;
        var reviewer = new PullRequestReviewer();

        var bad = reviewer.Review(Context(Added("src/Example.cs", doc.BadExample.Split('\n'))));
        var good = reviewer.Review(Context(Added("src/Example.cs", doc.GoodExample.Split('\n'))));

        Assert.Contains(bad.Findings, f => f.CheckId == id);
        Assert.DoesNotContain(good.Findings, f => f.CheckId == id);
    }

    [Fact]
    public void P004_Example_BehavesAsDocumented()
    {
        var config = RepoConfig.Parse("{\"scope\": [\"src/**\", \"tests/**\"]}").Config;
        var reviewer = new PullRequestReviewer();

        Assert.Contains(reviewer.Review(Context(config, Added("docs/notes.txt", "x"))).Findings, f => f.CheckId == "P004");
        Assert.DoesNotContain(reviewer.Review(Context(config, Added("src/Orders/OrderService.cs", "x"))).Findings, f => f.CheckId == "P004");
    }

    [Fact]
    public void Find_IsCaseInsensitive_AndSeparateFromRuleDocs()
    {
        Assert.Equal("TodoStubs", PullRequestCheckDocs.Find("p002")?.Name);
        Assert.Null(RuleDocs.Find("P002"));
        Assert.Null(PullRequestCheckDocs.Find("R001"));
    }
}
