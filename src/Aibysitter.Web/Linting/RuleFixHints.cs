using System.Collections.Concurrent;
using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Rules.Repo;
using Aibysitter.Rules.Rules;

namespace Aibysitter.Web.Linting;

/// <summary>
/// Fix hints for rule pages, taken from what each rule emits on its own doc's bad example, so the page cannot drift from
/// the code. Lint rules run through the engine, P checks through the reviewer, R006 against a fixed repository snapshot.
/// </summary>
public sealed class RuleFixHints(LintEngine engine, PullRequestReviewer reviewer)
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> For(string id) => cache.GetOrAdd(id, Compute);

    private IReadOnlyList<string> Compute(string id)
    {
        if (RuleDocs.Find(id) is { } ruleDoc)
        {
            return ruleDoc.AppOnly ? MissingIdentifierHints(ruleDoc) : Distinct(engine.Lint(BadText(ruleDoc)).Where(f => f.RuleId == ruleDoc.Id).Select(f => f.FixHint));
        }

        if (PullRequestCheckDocs.Find(id) is { } checkDoc)
        {
            return Distinct(reviewer.Review(CheckContext(checkDoc)).Findings.Where(f => f.CheckId == checkDoc.Id).Select(f => f.FixHint));
        }

        return [];
    }

    /// <summary>The doc's bad example; R004's describes a long file, so one line over the limit is generated.</summary>
    private static string BadText(RuleDoc doc) =>
        doc.Id == "R004" ? string.Join("\n", Enumerable.Range(1, FileLength.MaxLines + 1).Select(i => $"- Line {i}.")) : doc.BadExample;

    /// <summary>R006 against a repository shaped like the doc's good example: a lint script and src/lib/hooks/.</summary>
    private static IReadOnlyList<string> MissingIdentifierHints(RuleDoc doc)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["package.json"] = "{ \"scripts\": { \"lint\": \"eslint .\" } }",
            ["src/lib/hooks/useCart.ts"] = "export {};",
        };
        var snapshot = new RepoSnapshot(files.Keys, path => files.GetValueOrDefault(path));
        return Distinct(new MissingIdentifiers().Evaluate(RulesFile.Parse(doc.BadExample), "CLAUDE.md", snapshot).Select(f => f.FixHint));
    }

    /// <summary>A pull request that reproduces the doc's bad example; path- and config-based checks get their own shape.</summary>
    private static PullRequestContext CheckContext(RuleDoc doc) => doc.Id switch
    {
        "P004" => new([Added("docs/notes.txt", "x")], RepoConfig.Parse("{\"scope\": [\"src/**\", \"tests/**\"]}").Config),
        "P008" => Default(new ChangedFile("tests/Orders/OrderServiceTests.cs", FileChangeStatus.Removed, "@@ -1,1 +0,0 @@\n-public class OrderServiceTests { }")),
        "P010" => Default(Added("src/Api/Api.csproj", doc.BadExample)),
        "P011" => Default(new ChangedFile(".github/workflows/ci.yml", FileChangeStatus.Modified, "@@ -1,1 +1,1 @@\n-name: CI\n+name: Build", HeadContent: "name: Build")),
        "P013" => Default(Added("src/Api/bin/Debug/net10.0/Api.dll", "x"), Added(".env", "API_KEY=1")),
        "P014" => Default(Added("CLAUDE.md", "- Handle errors properly.")),
        "P015" => Default(Added(".github/workflows/ci.yml", doc.BadExample)),
        "P019" => Default(Added("src/Api/appsettings.json", doc.BadExample)),
        _ => Default(Added("src/Example.cs", doc.BadExample.Split('\n'))),
    };

    private static PullRequestContext Default(params ChangedFile[] files) => new(files, RepoConfig.Default);

    private static ChangedFile Added(string path, params string[] lines) =>
        new(path, FileChangeStatus.Added, $"@@ -0,0 +1,{lines.Length} @@\n" + string.Join("\n", lines.Select(l => "+" + l)), HeadContent: string.Join("\n", lines));

    private static IReadOnlyList<string> Distinct(IEnumerable<string> hints) => hints.Distinct(StringComparer.Ordinal).ToList();
}
