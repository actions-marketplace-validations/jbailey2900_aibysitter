using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// Vague verbs in instruction position (clause start, optionally after a modal), and vague
/// qualifiers in imperative clauses. Skips table rows, inline code, links, and paths, and terms
/// followed by a concrete target in the same clause.
/// </summary>
public sealed partial class VagueVerbs : IRule
{
    public string Id => "R002";
    public string Title => "Vague verbs";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        foreach (var line in file.Lines.Where(l => l.IsProse && !InstructionText.IsTableRow(l.Text)))
        {
            var terms = new List<string>();
            foreach (var clause in InstructionText.Clauses(line.Text))
            {
                var verb = LeadVerbRegex().Match(clause);
                if (verb.Success
                    && clause[(verb.Index + verb.Length)..].Trim().Length > 0
                    && !InstructionText.HasConcreteTarget(clause[(verb.Index + verb.Length)..])
                    && !(verb.Groups["term"].Value.Equals("ensure", StringComparison.OrdinalIgnoreCase) && CheckableObjectRegex().IsMatch(clause[(verb.Index + verb.Length)..])))
                {
                    terms.Add(verb.Groups["term"].Value.ToLowerInvariant());
                }

                if (ImperativeRegex().IsMatch(clause))
                {
                    foreach (Match q in QualifierRegex().Matches(clause))
                    {
                        if (!InstructionText.HasConcreteTarget(clause[(q.Index + q.Length)..]))
                        {
                            terms.Add(q.Value.ToLowerInvariant());
                        }
                    }
                }
            }

            if (terms.Count > 0)
            {
                yield return new Finding(
                    Id,
                    line.Number,
                    $"Vague wording: {string.Join(", ", terms.Distinct().Select(t => $"\"{t}\""))}",
                    "Name the concrete action, file, or command.");
            }
        }
    }

    private const string Modals = @"(?:(?:always|must|should|never|please|also|then|and|or)\s+)*(?:(?:do\s+not|don't|make\s+sure\s+to)\s+)?";

    [GeneratedRegex("^" + Modals + @"(?<term>handle|manage|deal\s+with|ensure|improve|optimize(?!\s+for\b)|clean\s+up)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LeadVerbRegex();

    /// <summary>Clause opens with a modal or a common imperative verb.</summary>
    [GeneratedRegex(@"^(?:(?:always|must|should|never|please|do\s+not|don't)\b|(?:use|add|run|create|check|keep|avoid|follow|write|call|set|make|prefer|return|update|verify|validate|select|export|edit|assign|move|organize|configure|cache|continue|read|merge|adapt|wrap|log|put|place|define|store|import|include|apply|choose|pick|install|build|deploy|commit|review|refactor|implement|handle|manage|ensure|treat|throw|catch|raise|split|sort|mark|limit|scale|tune|register|inject|convert|escape|sanitize|encode|close|dispose|release|retry|notify|load|save|fetch|send|clean|remove|delete|replace|rename|extend|override|reuse|share|configure|enable|disable|initialize|init|stop)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex ImperativeRegex();

    /// <summary>Qualifiers. "where / when / if / as appropriate" is a hedge (R007), not matched here.</summary>
    [GeneratedRegex(@"(?<!\b(?:where|when|if|as|whenever)\s+)(?<!-)\b(?:properly|appropriate(?:ly)?|as\s+needed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualifierRegex();

    /// <summary>"ensure" followed by a statement with its own verb ("is set", "are not returned", "has", "pass") is checkable.</summary>
    [GeneratedRegex(@"\b(?:is|are|was|were|has|have|can|cannot|does|do|passes|pass|forms?|works?|matches|match|exists?|returns?|stays?|remains?|contains?|not|no)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CheckableObjectRegex();
}
