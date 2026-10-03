using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// Vague verbs in instruction position (clause start, optionally after a modal), and vague qualifiers in imperative
/// clauses, on lines an instruction sentence overlaps (<see cref="InstructionText.InstructionLines"/>). Skips table rows,
/// inline code, links, and paths; terms followed by a concrete target in the same clause; "clean up / handle / manage"
/// followed by a named resource (files, folders, branches, containers, processes, temp data, listeners); "ensure" after
/// inline code on the line, or followed by a statement with its own verb; qualifiers in an "ensure" clause after inline
/// code, followed by a gerund ("properly functioning") or a stated purpose or condition ("as needed for …"), or after
/// "more / most / less / least".
/// </summary>
public sealed partial class VagueVerbs : IRule
{
    public string Id => "R002";
    public string Title => "Vague verbs";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var instructionLines = InstructionText.InstructionLines(file);
        foreach (var line in file.Lines.Where(l => instructionLines.Contains(l.Number)))
        {
            var terms = new List<string>();
            var codeSeen = false;
            foreach (var clause in InstructionText.Clauses(line.Text))
            {
                var verb = LeadVerbRegex().Match(clause);
                var rest = verb.Success ? clause[(verb.Index + verb.Length)..] : string.Empty;
                var term = verb.Groups["term"].Value.ToLowerInvariant();
                var isEnsure = term == "ensure";
                if (verb.Success
                    && rest.Trim().Length > 0
                    && !InstructionText.HasConcreteTarget(rest)
                    && !ResourceObjectRegex().IsMatch(rest)
                    && !(isEnsure && (codeSeen || CheckableObjectRegex().IsMatch(rest))))
                {
                    terms.Add(term);
                }

                if (ImperativeRegex().IsMatch(clause) && !(isEnsure && codeSeen))
                {
                    foreach (Match q in QualifierRegex().Matches(clause))
                    {
                        var after = clause[(q.Index + q.Length)..];
                        if (!InstructionText.HasConcreteTarget(after) && !QualifierContextRegex().IsMatch(after))
                        {
                            terms.Add(q.Value.ToLowerInvariant());
                        }
                    }
                }

                codeSeen |= clause.Contains("CODE", StringComparison.Ordinal);
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
    [GeneratedRegex(@"(?<!\b(?:where|when|if|as|whenever|more|most|less|least)\s+)(?<!-)\b(?:properly|appropriate(?:ly)?|as\s+needed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QualifierRegex();

    /// <summary>
    /// "ensure" followed by a statement with its own verb ("is set", "are not returned", "has", "pass", "starts")
    /// is checkable.
    /// </summary>
    [GeneratedRegex(@"\b(?:is|are|was|were|has|have|can|cannot|does|do|passes|pass|forms?|works?|matches|match|exists?|returns?|stays?|remains?|contains?|not|no|successfully|starts|runs|builds|compiles|succeeds)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CheckableObjectRegex();

    /// <summary>A named resource after "clean up / handle / manage".</summary>
    [GeneratedRegex(@"^\s*(?:\S+\s+){0,3}(?:files?|folders?|director(?:y|ies)|branch(?:es)?|containers?|processes|worktrees?|temp|tmp|volumes?|subscriptions?|listeners?|timers?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ResourceObjectRegex();

    /// <summary>After a qualifier: a gerund ("properly functioning"), or a stated purpose or condition ("as needed for …").</summary>
    [GeneratedRegex(@"^\s*(?:[a-z]+ing\b|(?:for|when|if|to|by|in)\s+\S+\s+\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex QualifierContextRegex();
}
