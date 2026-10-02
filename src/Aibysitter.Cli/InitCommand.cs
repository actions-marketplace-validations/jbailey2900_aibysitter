using Aibysitter.Packs;
using Aibysitter.Rules;

namespace Aibysitter.Cli;

/// <summary><c>aibysitter init --packs a,b --format claude [--title T] [--output PATH] [--force]</c> and <c>aibysitter packs</c>.</summary>
internal static class InitCommand
{
    private sealed record Options(IReadOnlyList<string> Packs, RulesFormat Format, string? Title, string? Output, bool Force);

    public static int Packs(IReadOnlyList<string> args, TextWriter stdout, Func<string, int> usageFail)
    {
        if (args.Count > 0)
        {
            return usageFail($"packs takes no arguments; got \"{args[0]}\".");
        }

        var catalog = new PackCatalog();
        var width = catalog.All.Max(p => p.Id.Length);
        foreach (var pack in catalog.All)
        {
            var targets = pack.Manifest.Targets.Select(t => PackComposer.ShortNames.First(s => s.Value.ToString() == t).Key);
            stdout.WriteLine($"{pack.Id.PadRight(width)}  {pack.Manifest.Title} ({string.Join(", ", targets)})");
        }

        return CliApp.Ok;
    }

    public static int Init(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, Func<string, int> usageFail)
    {
        var catalog = new PackCatalog();
        var (options, error) = Parse(args, catalog);
        if (options is null)
        {
            return usageFail(error!);
        }

        var packs = options.Packs.Select(id => catalog.Find(id)!).ToList();
        var path = options.Output ?? PackComposer.DefaultPath(options.Format, packs);
        var text = PackComposer.Compose(packs, options.Format, options.Title);

        try
        {
            if (File.Exists(path) && !options.Force)
            {
                stderr.WriteLine($"aibysitter: {path} exists; use --force to overwrite it.");
                return CliApp.FileError;
            }

            if (Directory.Exists(path))
            {
                throw new IOException("is a directory");
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"aibysitter: cannot write {path}: {(ex is UnauthorizedAccessException ? "access denied" : ex.Message)}");
            return CliApp.FileError;
        }

        var report = LintReport.Create(new LintEngine(), text, options.Format, []);
        stdout.WriteLine($"Wrote {path} ({string.Join(", ", options.Packs)}): {report.Score}/100 {report.Grade} ({RulesFormats.DisplayName(options.Format)}, ruleset v{report.RulesetVersion})");
        return CliApp.Ok;
    }

    private static (Options? Options, string? Error) Parse(IReadOnlyList<string> args, PackCatalog catalog)
    {
        List<string>? packs = null;
        RulesFormat? format = null;
        string? title = null, output = null;
        var force = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            var (name, inline) = arg.IndexOf('=') is var eq and > 0 ? (arg[..eq], arg[(eq + 1)..]) : (arg, null);
            string? Value() => inline ?? (i + 1 < args.Count ? args[++i] : null);

            switch (name)
            {
                case "--packs":
                    var ids = Value();
                    if (string.IsNullOrWhiteSpace(ids))
                    {
                        return (null, "--packs needs pack IDs, for example --packs starter,aspnet-web-api.");
                    }

                    packs = ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
                    break;
                case "--format":
                    var formatName = Value();
                    if (formatName is null || !PackComposer.TryParseFormat(formatName, out var parsed))
                    {
                        return (null, $"--format needs one of: {string.Join(", ", PackComposer.ShortNames.Keys)}.");
                    }

                    format = parsed;
                    break;
                case "--title":
                    title = Value();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return (null, "--title needs text.");
                    }

                    break;
                case "--output":
                    output = Value();
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return (null, "--output needs a file path.");
                    }

                    break;
                case "--force" when inline is null:
                    force = true;
                    break;
                default:
                    return (null, arg.StartsWith('-') ? $"Unknown option \"{arg}\"." : $"init takes options only; got \"{arg}\".");
            }
        }

        if (packs is null)
        {
            return (null, "init needs --packs. Run 'aibysitter packs' to list them.");
        }

        if (format is null)
        {
            return (null, $"init needs --format: {string.Join(", ", PackComposer.ShortNames.Keys)}.");
        }

        var unknown = packs.Where(id => catalog.Find(id) is null).ToList();
        if (unknown.Count > 0)
        {
            return (null, $"Unknown pack: {string.Join(", ", unknown)}. Run 'aibysitter packs' to list them.");
        }

        var unsupported = packs.Where(id => !catalog.Find(id)!.Manifest.Targets.Contains(format.Value.ToString())).ToList();
        if (unsupported.Count > 0)
        {
            return (null, $"Pack {string.Join(", ", unsupported)} does not target {RulesFormats.DisplayName(format.Value)}.");
        }

        return (new Options(packs, format.Value, title, output, force), null);
    }
}
