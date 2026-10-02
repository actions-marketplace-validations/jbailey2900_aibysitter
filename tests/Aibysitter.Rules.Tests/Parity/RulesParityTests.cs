using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aibysitter.Rules.Browser;
using Xunit.Abstractions;

namespace Aibysitter.Rules.Tests.Parity;

/// <summary>The browser engine (wwwroot/js/lint-engine.mjs) must match the C# engine exactly.</summary>
public class RulesParityTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly LintEngine Engine = new();

    [Fact]
    public void BrowserEngine_MatchesCSharp_OnEveryParityInput()
    {
        if (NodeRunner.FindNodeOrSkip(output) is not { } node)
        {
            return;
        }

        var request = JsonSerializer.Serialize(ParityInputs.All.Select(i => new { name = i.Name, text = i.Text, format = i.Format.ToString() }));
        var actual = JsonNode.Parse(NodeRunner.Run(node, "lint", request))!.AsArray();

        var mismatches = ParityInputs.All.Zip(actual)
            .Where(pair => !JsonNode.DeepEquals(Expected(pair.First), pair.Second))
            .Select(pair => $"{pair.First.Name}\n  C#: {Expected(pair.First).ToJsonString()}\n  JS: {pair.Second!.ToJsonString()}")
            .ToList();

        Assert.Equal(ParityInputs.All.Count, actual.Count);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} of {actual.Count} inputs differ:\n" + string.Join("\n", mismatches));
        output.WriteLine($"{actual.Count} inputs identical.");
    }

    [Fact]
    public void EveryExportedPattern_CompilesInNode_AndMatchesLikeDotNet()
    {
        if (NodeRunner.FindNodeOrSkip(output) is not { } node)
        {
            return;
        }

        var regexes = DotNetRegexes();
        var lines = ParityInputs.All.SelectMany(i => i.Text.Replace("\r\n", "\n").Split('\n', '\r')).Distinct().ToList();
        var request = JsonSerializer.Serialize(new { patterns = regexes.Keys, lines });
        var actual = JsonNode.Parse(NodeRunner.Run(node, "patterns", request))!.AsObject();

        var mismatches = regexes
            .Where(r => !JsonNode.DeepEquals(Positions(r.Value, lines), actual[r.Key]))
            .Select(r => $"{r.Key}: {FirstDifference(r.Value, lines, actual[r.Key])}")
            .ToList();

        Assert.Equal(PatternExport.Patterns().Keys.Order(StringComparer.Ordinal), regexes.Keys.Order(StringComparer.Ordinal));
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    private static JsonNode Expected(ParityInput input)
    {
        var result = Engine.Analyze(input.Text, input.Format);
        return JsonSerializer.SerializeToNode(new
        {
            name = input.Name,
            format = result.Format.ToString(),
            findings = result.Findings,
            suppressed = result.Suppressed,
            score = ScoreShape(Engine.Score(result.Findings)),
        }, Json)!;
    }

    private static object ScoreShape(LintScore score) => new
    {
        value = score.Value,
        grade = score.Grade,
        deductionsByRule = score.DeductionsByRule.Select(d => new
        {
            ruleId = d.Key,
            severity = Engine.Rules.Single(r => r.Id == d.Key).Severity.ToString(),
            points = d.Value,
        }),
        deductionsBySeverity = score.DeductionsBySeverity.Select(d => new { severity = d.Key.ToString(), total = d.Value.Total, applied = d.Value.Applied }),
    };

    private static Dictionary<string, Regex> DotNetRegexes() =>
        typeof(LintEngine).Assembly.GetTypes()
            .Where(t => t.Namespace is "Aibysitter.Rules" or "Aibysitter.Rules.Rules" && !t.IsNested)
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<GeneratedRegexAttribute>() is not null)
                .Select(m => (Key: $"{t.Name}.{m.Name}", Regex: (Regex)m.Invoke(null, null)!)))
            .ToDictionary(x => x.Key, x => x.Regex, StringComparer.Ordinal);

    private static JsonNode Positions(Regex regex, IReadOnlyList<string> lines) =>
        JsonSerializer.SerializeToNode(lines.Select(line => regex.Matches(line).Select(m => new[] { m.Index, m.Length })))!;

    private static string FirstDifference(Regex regex, IReadOnlyList<string> lines, JsonNode? actual)
    {
        if (actual is JsonObject error)
        {
            return error.ToJsonString();
        }

        var expected = Positions(regex, lines).AsArray();
        var index = Enumerable.Range(0, lines.Count).First(i => !JsonNode.DeepEquals(expected[i], actual![i]));
        return $"line {JsonSerializer.Serialize(lines[index])}: .NET {expected[index]!.ToJsonString()}, JS {actual![index]!.ToJsonString()}";
    }
}
