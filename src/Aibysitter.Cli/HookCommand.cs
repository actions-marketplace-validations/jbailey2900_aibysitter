using System.Text.Json;
using Aibysitter.Rules;

namespace Aibysitter.Cli;

/// <summary><c>aibysitter hook claude-code</c>: never fails the tool call except to report findings (exit 2).</summary>
internal static class HookCommand
{
    public const string ArgsVariable = "AIBYSITTER_HOOK_ARGS";
    public const int Feedback = 2;

    public static int ClaudeCode(IReadOnlyList<string> args, TextReader stdin, TextWriter stderr, Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;
        var all = args.Concat((environment(ArgsVariable) ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
        var disable = new List<string>();
        for (var i = 0; i < all.Count; i++)
        {
            var (name, inline) = all[i].IndexOf('=') is var eq and > 0 ? (all[i][..eq], all[i][(eq + 1)..]) : (all[i], null);
            var value = name == "--disable" ? inline ?? (i + 1 < all.Count ? all[++i] : null) : null;
            if (value is null)
            {
                stderr.WriteLine($"aibysitter hook: ignored argument \"{all[i]}\"; only --disable is supported.");
                continue;
            }

            disable.AddRange(value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        try
        {
            using var doc = JsonDocument.Parse(stdin.ReadToEnd());
            if (!doc.RootElement.TryGetProperty("tool_input", out var input)
                || input.ValueKind != JsonValueKind.Object
                || !input.TryGetProperty("file_path", out var pathElement)
                || pathElement.GetString() is not { Length: > 0 } path)
            {
                return CliApp.Ok;
            }

            var cwd = doc.RootElement.TryGetProperty("cwd", out var cwdElement) ? cwdElement.GetString() : null;
            var full = Path.IsPathRooted(path) || cwd is null ? path : Path.Combine(cwd, path);
            var display = cwd is not null && Path.IsPathRooted(full) ? Path.GetRelativePath(cwd, full).Replace('\\', '/') : path;
            if (PathFormat.Resolve(full) is not { } format || !File.Exists(full))
            {
                return CliApp.Ok;
            }

            var engine = new LintEngine();
            if (!LintReport.TryNormalizeDisabled(engine, disable, out var disabled, out var unknown))
            {
                stderr.WriteLine($"aibysitter hook: not a lint rule ID: {string.Join(", ", unknown)}; ignored.");
                LintReport.TryNormalizeDisabled(engine, disable.Except(unknown, StringComparer.OrdinalIgnoreCase), out disabled, out _);
            }

            var report = LintReport.Create(engine, File.ReadAllText(full).TrimStart('\uFEFF'), format, disabled);
            var toFix = report.Findings.Where(f => f.Severity is nameof(Severity.Error) or nameof(Severity.Warning)).ToList();
            if (toFix.Count == 0)
            {
                return CliApp.Ok;
            }

            stderr.WriteLine($"aibysitter: {display} has {toFix.Count} finding{(toFix.Count == 1 ? "" : "s")} to fix (score {report.Score}/100 {report.Grade}):");
            foreach (var f in toFix)
            {
                stderr.WriteLine($"{display}:{f.Line} {f.Rule} {f.Severity}: {f.Message} Fix: {f.FixHint}");
            }

            return Feedback;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return CliApp.Ok;
        }
    }
}
