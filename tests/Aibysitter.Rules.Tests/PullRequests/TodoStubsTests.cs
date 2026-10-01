using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class TodoStubsTests
{
    private readonly TodoStubs check = new();

    [Theory]
    [InlineData("src/A.cs", "throw new NotImplementedException();")]
    [InlineData("src/A.cs", "    throw new NotImplementedException(\"later\");")]
    [InlineData("src/A.cs", "// TODO: wire up retries")]
    [InlineData("src/A.cs", "/* FIXME */")]
    [InlineData("app/a.py", "raise NotImplementedError")]
    [InlineData("app/a.py", "pass  # TODO")]
    [InlineData("src/lib.rs", "todo!()")]
    [InlineData("src/lib.rs", "unimplemented!(\"x\")")]
    [InlineData("db/up.sql", "-- TODO add index")]
    [InlineData("web/a.ts", "return null; // TODO")]
    public void Flags(string path, string line)
    {
        var finding = Assert.Single(check.Evaluate(Context(Added(path, line))));

        Assert.Equal("P002", finding.CheckId);
        Assert.Equal(1, finding.Line);
    }

    [Theory]
    [InlineData("src/A.cs", "catch (NotImplementedException) { }")]
    [InlineData("src/A.cs", "// Todo list feature")]
    [InlineData("src/A.cs", "var todo = new TodoItem();")]
    [InlineData("src/A.cs", "#region TODOs")]
    [InlineData("README.md", "// TODO in docs")]
    public void DoesNotFlag(string path, string line)
    {
        Assert.Empty(check.Evaluate(Context(Added(path, line))));
    }
}
