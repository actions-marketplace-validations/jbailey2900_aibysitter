using Aibysitter.Rules;

namespace Aibysitter.Cli;

/// <param name="Path">File path, or <see cref="LintOptions.Stdin"/>.</param>
/// <param name="Format">Set by <c>--format</c>; null resolves from the path, then from content.</param>
/// <param name="FailBelow">Grade letter from <c>--fail-below</c>.</param>
internal sealed record LintOptions(
    string Path,
    RulesFormat? Format,
    IReadOnlyList<string> Disable,
    bool Json,
    bool FailOnError,
    char? FailBelow)
{
    public const string Stdin = "-";

    public static readonly IReadOnlyList<char> Grades = ['A', 'B', 'C', 'D'];

    /// <summary>Options for <c>lint</c>, or an error message for exit code 2.</summary>
    public static (LintOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        string? path = null;
        RulesFormat? format = null;
        var disable = new List<string>();
        bool json = false, failOnError = false, optionsEnded = false;
        char? failBelow = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (optionsEnded || arg == Stdin || !arg.StartsWith('-'))
            {
                if (path is not null)
                {
                    return (null, $"lint takes one file; got \"{path}\" and \"{arg}\".");
                }

                path = arg;
                continue;
            }

            var (name, inline) = arg.IndexOf('=') is var eq and > 0 ? (arg[..eq], arg[(eq + 1)..]) : (arg, null);
            string? Value()
            {
                if (inline is not null)
                {
                    return inline;
                }

                return i + 1 < args.Count ? args[++i] : null;
            }

            switch (name)
            {
                case "--":
                    optionsEnded = true;
                    break;
                case "--json" when inline is null:
                    json = true;
                    break;
                case "--fail-on-error" when inline is null:
                    failOnError = true;
                    break;
                case "--format":
                    var formatName = Value();
                    if (formatName is null || !RulesFormats.TryParse(formatName, out var parsed))
                    {
                        return (null, $"--format needs one of: {string.Join(", ", Enum.GetNames<RulesFormat>())}.");
                    }

                    format = parsed;
                    break;
                case "--disable":
                    var ids = Value();
                    if (string.IsNullOrWhiteSpace(ids))
                    {
                        return (null, "--disable needs rule IDs, for example --disable R002,R005.");
                    }

                    disable.AddRange(ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    break;
                case "--fail-below":
                    var grade = Value()?.Trim().ToUpperInvariant();
                    if (grade is not { Length: 1 } || !Grades.Contains(grade[0]))
                    {
                        return (null, "--fail-below needs a grade: A, B, C or D.");
                    }

                    failBelow = grade[0];
                    break;
                default:
                    return (null, $"Unknown option \"{arg}\".");
            }
        }

        return path is null
            ? (null, "lint needs a file path, or - to read standard input.")
            : (new LintOptions(path, format, disable, json, failOnError, failBelow), null);
    }
}
