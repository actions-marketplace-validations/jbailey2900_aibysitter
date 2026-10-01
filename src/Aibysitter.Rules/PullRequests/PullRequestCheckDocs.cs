namespace Aibysitter.Rules.PullRequests;

public static class PullRequestCheckDocs
{
    public static IReadOnlyList<RuleDoc> All { get; } =
    [
        new(
            "P001",
            "PlaceholderIdentifiers",
            "A change leaves a placeholder where a real identifier or value belongs.",
            "Added lines in code and config files containing \"::NAME::\", \"REPLACE_ME\", \"YOUR_…\", \"<placeholder>\", \"<your-…>\", or \"TODO_…\".",
            "var apiKey = \"YOUR_API_KEY\";",
            "var apiKey = configuration[\"Billing:ApiKey\"];"),
        new(
            "P002",
            "TodoStubs",
            "A change adds a stub or a TODO where the implementation belongs.",
            "Added \"throw new NotImplementedException(\", \"raise NotImplementedError\", \"todo!()\", \"unimplemented!()\", and TODO or FIXME comments in code files.",
            "public decimal Total() => throw new NotImplementedException();",
            "public decimal Total() => Lines.Sum(line => line.Amount);"),
        new(
            "P003",
            "AssertNothingTests",
            "A test runs code but asserts nothing about the result.",
            "C# only: xUnit, NUnit, and MSTest test methods touched by the pull request with no \"Assert\", \"Should\", \"Verify\", \"Received\", or \"Expect\" call.",
            "[Fact]\npublic void Add_Works()\n{\n    var sum = new Calculator().Add(2, 3);\n}",
            "[Fact]\npublic void Add_ReturnsSum()\n{\n    Assert.Equal(5, new Calculator().Add(2, 3));\n}"),
        new(
            "P004",
            "OutOfScopeFiles",
            "A change touches files outside the scope declared in .github/aibysitter.json.",
            "Changed, renamed, or removed files whose path matches none of the \"scope\" globs. Runs only when \"scope\" is set.",
            "scope:   src/**, tests/**\nchanged: docs/notes.txt",
            "scope:   src/**, tests/**\nchanged: src/Orders/OrderService.cs"),
    ];

    private static readonly Dictionary<string, RuleDoc> ById = All.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public static RuleDoc? Find(string id) => ById.GetValueOrDefault(id);
}
