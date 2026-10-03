using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// Too many emphasis lines: more than <see cref="PerHundredLines"/> per 100 lines, minimum allowance
/// <see cref="MinAllowed"/>. Reported once, at the first line over the limit. In files that use RFC 2119 keywords
/// (capitalized SHOULD, or a mention of RFC 2119 or BCP 14), MUST and REQUIRED are keywords, not emphasis.
/// </summary>
public sealed partial class EmphasisInflation : IRule
{
    public const int PerHundredLines = 3;
    public const int MinAllowed = 3;

    public string Id => "R008";
    public string Title => "Emphasis inflation";
    public Severity Severity => Severity.Warning;

    public static int Allowed(int lineCount) => Math.Max(MinAllowed, (int)Math.Ceiling(PerHundredLines * lineCount / 100.0));

    public static bool IsEmphasisLine(string text, bool rfcKeywords = false) =>
        (rfcKeywords ? EmphasisWithoutKeywordsRegex() : EmphasisRegex()).IsMatch(InstructionText.WithoutCode(text));

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var counted = file.Lines.Where(l => l.IsProse || l.IsHeading).ToList();
        var rfcKeywords = counted.Any(l => RfcKeywordsRegex().IsMatch(InstructionText.WithoutCode(l.Text)));
        var emphasis = counted.Where(l => IsEmphasisLine(l.Text, rfcKeywords)).ToList();
        var allowed = Allowed(file.Lines.Count);

        if (emphasis.Count > allowed)
        {
            yield return new Finding(
                Id,
                emphasis[allowed].Number,
                $"{emphasis.Count} emphasis lines in {file.Lines.Count} lines; limit is {allowed}.",
                "Keep emphasis for the few rules that override others. State the rest plainly.");
        }
    }

    /// <summary>Capitalized IMPORTANT / CRITICAL / MUST / NEVER / ALWAYS / DO NOT / MANDATORY / REQUIRED, or "!!".</summary>
    [GeneratedRegex(@"\b(?:IMPORTANT|CRITICAL|MUST|NEVER|ALWAYS|DO NOT|MANDATORY|REQUIRED)\b|!!")]
    private static partial Regex EmphasisRegex();

    [GeneratedRegex(@"\b(?:IMPORTANT|CRITICAL|NEVER|ALWAYS|DO NOT|MANDATORY)\b|!!")]
    private static partial Regex EmphasisWithoutKeywordsRegex();

    [GeneratedRegex(@"\bSHOULD\b|\bRFC\s?2119\b|\bBCP\s?14\b")]
    private static partial Regex RfcKeywordsRegex();
}
