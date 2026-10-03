using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public sealed partial class DebugLeftovers : AddedLinePatternCheck
{
    public override string Id => "P012";
    public override string Title => "Debug leftovers";
    public override Severity Severity => Severity.Info;

    protected override Regex Pattern => DebugRegex();
    protected override string MessagePrefix => "Debug statement";
    protected override string FixHint => "Remove it, or use the project's logger.";

    protected override bool AppliesTo(string path) => FileKinds.IsCode(path) && !FileKinds.IsTestFile(path);

    /// <summary>print( in Python files under a scripts/ or tools/ folder is program output, not a finding.</summary>
    protected override IEnumerable<string> Keep(ChangedFile file, string line, IReadOnlyList<string> matches) =>
        ScriptFolderRegex().IsMatch(file.Path.Replace('\\', '/'))
            ? matches.Where(m => !m.StartsWith("print", StringComparison.Ordinal))
            : matches;

    [GeneratedRegex(@"(?:^|/)(?:scripts|tools)/[^/]+(?:/[^/]+)*\.py$", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptFolderRegex();

    /// <summary>
    /// console.log / console.debug, debugger;, Debug.WriteLine, print(, breakpoint(), pdb.set_trace(), import pdb,
    /// binding.pry, Rust dbg!. Lines that are only a comment are skipped.
    /// </summary>
    [GeneratedRegex(@"^(?!\s*(?://|#(?!\[)|/\*|\*|--)).*?(?<m>\bconsole\.(?:log|debug)\s*\(|\bdebugger\s*;|\bDebug\.WriteLine\s*\(|(?<![\w.])print\s*\(|\bbreakpoint\s*\(\s*\)|\bpdb\.set_trace\s*\(|^\s*import\s+pdb\b|\bbinding\.pry\b|\bdbg!\s*\()")]
    private static partial Regex DebugRegex();
}
