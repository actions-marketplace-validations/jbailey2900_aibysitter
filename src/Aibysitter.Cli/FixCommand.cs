using System.Text;
using Aibysitter.Rules;

namespace Aibysitter.Cli;

/// <summary>
/// <c>aibysitter fix</c>: R011 and R012. Writes the file in place; <c>--dry-run</c> prints a unified diff and exits 1
/// when there are changes. With <c>-</c>, the fixed text (or the diff) goes to standard output and the summary to standard error.
/// </summary>
internal static class FixCommand
{
    public const int Changes = 1;

    public static int Run(IReadOnlyList<string> args, TextReader stdin, TextWriter stdout, TextWriter stderr, Func<string, int> usageFail)
    {
        string? path = null, stdinPath = null;
        RulesFormat? format = null;
        var dryRun = false;
        var disable = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg == LintOptions.Stdin || !arg.StartsWith('-'))
            {
                if (path is not null)
                {
                    return usageFail($"fix takes one file; got \"{path}\" and \"{arg}\".");
                }

                path = arg;
                continue;
            }

            var (name, inline) = arg.IndexOf('=') is var eq and > 0 ? (arg[..eq], arg[(eq + 1)..]) : (arg, null);
            string? Value() => inline ?? (i + 1 < args.Count ? args[++i] : null);
            switch (name)
            {
                case "--dry-run" when inline is null:
                    dryRun = true;
                    break;
                case "--disable":
                    var ids = Value();
                    if (string.IsNullOrWhiteSpace(ids))
                    {
                        return usageFail("--disable needs rule IDs, for example --disable R011.");
                    }

                    disable.AddRange(ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    break;
                case "--format":
                    var formatName = Value();
                    if (formatName is null || !RulesFormats.TryParse(formatName, out var parsed))
                    {
                        return usageFail($"--format needs one of: {string.Join(", ", Enum.GetNames<RulesFormat>())}.");
                    }

                    format = parsed;
                    break;
                case "--stdin-path":
                    stdinPath = Value();
                    if (string.IsNullOrWhiteSpace(stdinPath))
                    {
                        return usageFail("--stdin-path needs a path, for example --stdin-path CLAUDE.md.");
                    }

                    break;
                default:
                    return usageFail($"Unknown option \"{arg}\".");
            }
        }

        if (path is null)
        {
            return usageFail("fix needs a file path, or - to read standard input.");
        }

        var isStdin = path == LintOptions.Stdin;
        if (stdinPath is not null && !isStdin)
        {
            return usageFail("--stdin-path is for standard input; use - as the file.");
        }

        var engine = new LintEngine();
        if (!LintReport.TryNormalizeDisabled(engine, disable, out var disabled, out var unknown))
        {
            return usageFail($"Not a lint rule ID: {string.Join(", ", unknown)}.");
        }

        var label = isStdin ? stdinPath ?? CliApp.StdinLabel : path;
        string before;
        try
        {
            before = isStdin ? stdin.ReadToEnd() : new UTF8Encoding(false).GetString(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"aibysitter: cannot read {path}: {(ex is FileNotFoundException or DirectoryNotFoundException ? "not found" : ex is UnauthorizedAccessException ? "access denied" : ex.Message)}");
            return CliApp.FileError;
        }

        var resolved = format ?? (isStdin && stdinPath is null ? null : PathFormat.Resolve(label)) ?? RulesFormat.Auto;
        var result = RulesFileFixer.Fix(engine, before, resolved, disabled);
        var changed = result.Text != before;
        var summaryOut = isStdin ? stderr : stdout;

        if (dryRun)
        {
            stdout.Write(UnifiedDiff.Write(label, before, result.Text));
            summaryOut.WriteLine(Summary(engine, disabled, resolved, label, before, result, wouldFix: true));
            return changed ? Changes : CliApp.Ok;
        }

        if (isStdin)
        {
            stdout.Write(result.Text);
        }
        else if (changed)
        {
            try
            {
                File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(result.Text));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                stderr.WriteLine($"aibysitter: cannot write {path}: {(ex is UnauthorizedAccessException ? "access denied" : ex.Message)}");
                return CliApp.FileError;
            }
        }

        summaryOut.WriteLine(Summary(engine, disabled, resolved, label, before, result, wouldFix: false));
        return CliApp.Ok;
    }

    private static string Summary(LintEngine engine, IReadOnlyList<string> disabled, RulesFormat format, string label, string before, FixResult result, bool wouldFix)
    {
        if (result.Fixed.Count == 0)
        {
            return $"{label}: nothing to fix.";
        }

        var counts = string.Join(", ", result.Fixed.GroupBy(f => f.RuleId).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} ×{g.Count()}"));
        var active = engine.Without(disabled);
        int Score(string text) => active.Score(active.Analyze(text.TrimStart('﻿'), format).Findings).Value;
        return $"{label}: {(wouldFix ? "would fix" : "fixed")} {result.Fixed.Count} ({counts}); score {Score(before)} → {Score(result.Text)}.";
    }
}
