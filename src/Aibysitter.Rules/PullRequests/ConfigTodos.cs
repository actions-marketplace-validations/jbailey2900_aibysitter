using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>TBD, TODO or FIXME on added non-comment lines of config files (JSON, YAML, XML, .config, MSBuild, .env, TOML, INI).</summary>
public sealed partial class ConfigTodos : AddedLinePatternCheck
{
    public override string Id => "P019";
    public override string Title => "Unfinished config values";
    public override Severity Severity => Severity.Warning;

    protected override Regex Pattern => TodoRegex();
    protected override string MessagePrefix => "Unfinished value in config";
    protected override string FixHint => "Set the real value, or leave the setting out until it exists.";

    protected override bool AppliesTo(string path) => FileKinds.IsCodeOrConfig(path) && !FileKinds.IsCode(path);

    protected override IEnumerable<string> Keep(ChangedFile file, DiffLine line, IReadOnlyList<string> matches) =>
        CodeText.IsCommentOnly(line.Text) ? [] : matches;

    [GeneratedRegex(@"\b(?:TBD|TODO|FIXME)\b")]
    private static partial Regex TodoRegex();
}
