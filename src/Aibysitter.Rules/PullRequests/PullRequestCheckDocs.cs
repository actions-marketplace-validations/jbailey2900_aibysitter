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
        new(
            "P005",
            "SecretsInDiff",
            "A change adds a credential.",
            "Added lines in any file matching the R009 patterns: private key headers; AWS, GitHub, Slack, Anthropic, OpenAI, Stripe live and Google API keys; JSON Web Tokens; Password= and Pwd= values; credentials in URLs other than localhost. Placeholder values are ignored. Findings show a redacted prefix only.",
            "\"ConnectionStrings\": { \"Default\": \"Server=db;User Id=app;Password=Hunter2Prod;\" }",
            "\"ConnectionStrings\": { \"Default\": \"\" }"),
        new(
            "P006",
            "SkippedTests",
            "A change skips a test instead of fixing it.",
            "Added lines in code files: xUnit \"Skip =\"; NUnit and MSTest [Ignore]; it / test / describe .skip, xit, xtest, xdescribe; @pytest.mark.skip, pytest.skip(), @unittest.skip; Go t.Skip(); Rust #[ignore]; JUnit @Disabled and @Ignore. Conditional skips (skipif, skipIf, skipUnless) are not flagged.",
            "[Fact(Skip = \"flaky\")]\npublic void Total_IncludesTax()",
            "[Fact]\npublic void Total_IncludesTax()"),
        new(
            "P007",
            "SuppressedDiagnostics",
            "A change silences a compiler or linter warning.",
            "Added lines in code, config, and .editorconfig files: #pragma warning disable, [SuppressMessage], ReSharper disable, <NoWarn>, dotnet_diagnostic.*.severity = none; eslint-disable, @ts-ignore, @ts-nocheck; # noqa, # type: ignore, # pylint: disable, # pyright: ignore; //nolint; #[allow(...)]; @SuppressWarnings.",
            "#pragma warning disable CS8602\nvar name = customer.Name.Trim();",
            "var name = customer?.Name?.Trim() ?? string.Empty;"),
        new(
            "P013",
            "CommittedArtifacts",
            "A change adds build output, dependencies, caches, or an environment file.",
            "Added, renamed, or copied files under node_modules/, bin/Debug, bin/Release, obj/Debug, obj/Release, obj restore files, __pycache__/, *.pyc, and .env or .env.* (except .env.example, .env.sample, .env.template, .env.dist). A bin/ folder of scripts is not flagged.",
            "added: src/Api/bin/Debug/net10.0/Api.dll\nadded: .env",
            "added: .env.example\n.gitignore: bin/, obj/, .env"),
    ];

    private static readonly Dictionary<string, RuleDoc> ById = All.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public static RuleDoc? Find(string id) => ById.GetValueOrDefault(id);
}
