using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// C# only: flags xUnit/NUnit/MSTest test methods touched by the PR whose body contains no assertion.
/// A call to a method declared in the same file whose body asserts counts as an assertion (one level).
/// </summary>
public sealed partial class AssertNothingTests : IPullRequestCheck
{
    public string Id => "P003";
    public string Title => "Assert-nothing tests";
    public Severity Severity => Severity.Info;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status != FileChangeStatus.Removed && f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !CodeText.IsGenerated(f)))
        {
            var content = file.HeadContent ?? (file.Status == FileChangeStatus.Added ? string.Join("\n", file.AddedLines.Select(l => l.Text)) : null);
            if (content is null)
            {
                continue;
            }

            var added = file.AddedLines.Select(l => l.NewLine!.Value).ToHashSet();
            var lines = content.Replace("\r\n", "\n").Split('\n');
            var helpers = AssertingHelpers(lines);

            foreach (var method in FindTestMethods(lines))
            {
                if (method.Skipped || !added.Any(n => n >= method.StartLine && n <= method.EndLine))
                {
                    continue;
                }

                if (!AssertionRegex().IsMatch(method.Body) && !helpers.Any(h => CallsMethod(method.Body, h)))
                {
                    yield return new PullRequestFinding(
                        Id,
                        file.Path,
                        method.SignatureLine,
                        $"Test \"{method.Name}\" has no assertion.",
                        "Assert on the result, or delete the test.");
                }
            }
        }
    }

    private static IEnumerable<TestMethod> FindTestMethods(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (!TestAttributeRegex().IsMatch(lines[i]))
            {
                continue;
            }

            var start = i;
            var skipped = SkipRegex().IsMatch(lines[i]);

            var sig = i;
            while (sig < lines.Length && (lines[sig].TrimStart().StartsWith('[') || string.IsNullOrWhiteSpace(lines[sig])))
            {
                skipped |= SkipRegex().IsMatch(lines[sig]);
                sig++;
            }

            if (sig >= lines.Length)
            {
                yield break;
            }

            var name = MethodNameRegex().Match(lines[sig]) is { Success: true } m ? m.Groups[1].Value : "?";
            var (end, body) = ReadBody(lines, sig);

            yield return new TestMethod(name, start + 1, sig + 1, end + 1, body, skipped);
            i = end;
        }
    }

    /// <summary>Returns the last line index of the method and its body text (strings and comments blanked).</summary>
    private static (int End, string Body) ReadBody(string[] lines, int sig)
    {
        var depth = 0;
        var opened = false;
        var expression = false;
        var body = new System.Text.StringBuilder();

        for (var i = sig; i < lines.Length; i++)
        {
            var code = StripLiterals(lines[i]);
            var from = 0;

            if (i == sig)
            {
                var paren = code.IndexOf('(');
                from = paren < 0 ? 0 : paren;
            }

            for (var c = from; c < code.Length; c++)
            {
                if (!opened && !expression && c + 1 < code.Length && code[c] == '=' && code[c + 1] == '>')
                {
                    expression = true;
                    body.Append(code[(c + 2)..]).Append('\n');
                    break;
                }

                if (code[c] == '{' && !expression)
                {
                    depth++;
                    if (!opened)
                    {
                        opened = true;
                        continue;
                    }
                }
                else if (code[c] == '}' && opened)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return (i, body.ToString());
                    }
                }

                if (opened)
                {
                    body.Append(code[c]);
                }
            }

            if (expression)
            {
                if (i > sig)
                {
                    body.Append(code).Append('\n');
                }

                if (code.TrimEnd().EndsWith(';'))
                {
                    return (i, body.ToString());
                }

                continue;
            }

            if (opened)
            {
                body.Append('\n');
            }
        }

        return (lines.Length - 1, body.ToString());
    }

    private static string StripLiterals(string line)
    {
        var noLiterals = LiteralRegex().Replace(line, m => new string(' ', m.Length));
        return LineCommentRegex().Replace(noLiterals, string.Empty);
    }

    private sealed record TestMethod(string Name, int StartLine, int SignatureLine, int EndLine, string Body, bool Skipped);

    /// <summary>Names of methods declared in the file whose body contains an assertion.</summary>
    private static HashSet<string> AssertingHelpers(string[] lines)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < lines.Length; i++)
        {
            var declaration = MethodDeclarationRegex().Match(lines[i]);
            if (declaration.Success && AssertionRegex().IsMatch(ReadBody(lines, i).Body))
            {
                names.Add(declaration.Groups["name"].Value);
            }
        }

        return names;
    }

    private static bool CallsMethod(string body, string name) =>
        Regex.IsMatch(body, $@"(?<![\w.])(?:this\.)?{Regex.Escape(name)}\s*(?:<[^>]*>)?\s*\(");

    [GeneratedRegex(@"^\s*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|new|unsafe|extern)\s+)+[\w<>\[\],.?() ]+?\s+(?<name>[A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(")]
    private static partial Regex MethodDeclarationRegex();

    [GeneratedRegex(@"^\s*\[\s*(?:Xunit\.)?(?:Fact|Theory|Test|TestCase|TestCaseSource|TestMethod|DataTestMethod)\b")]
    private static partial Regex TestAttributeRegex();

    [GeneratedRegex(@"\bSkip\s*=")]
    private static partial Regex SkipRegex();

    [GeneratedRegex(@"\b([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>]*>)?\s*\(")]
    private static partial Regex MethodNameRegex();

    [GeneratedRegex(@"\bAssert\w*\s*\.|\bAssert\w*\s*\(|\.Should\w*\s*\(|\bVerify\w*\s*\(|\.Received\w*\s*\(|\bExpect\w*\s*\(")]
    private static partial Regex AssertionRegex();

    [GeneratedRegex(@"@?""(?:[^""\\]|\\.|"""")*""|'(?:[^'\\]|\\.)'")]
    private static partial Regex LiteralRegex();

    [GeneratedRegex(@"//.*$")]
    private static partial Regex LineCommentRegex();
}
