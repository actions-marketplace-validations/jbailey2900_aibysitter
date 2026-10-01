using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

public sealed partial class ContradictoryModals : IRule
{
    public string Id => "R003";
    public string Title => "Contradictory modals";
    public Severity Severity => Severity.Error;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var seen = new Dictionary<(bool Positive, string Remainder), int>();

        foreach (var line in file.Lines.Where(l => l.IsProse))
        {
            var match = ModalRegex().Match(TextNormalizer.Normalize(line.Text));
            if (!match.Success)
            {
                continue;
            }

            var positive = match.Groups["pos"].Success;
            var remainder = match.Groups["rest"].Value.TrimEnd('.', '!', ';', ':', ' ');
            if (remainder.Length == 0)
            {
                continue;
            }

            if (seen.TryGetValue((!positive, remainder), out var earlier))
            {
                yield return new Finding(
                    Id,
                    line.Number,
                    $"Contradicts line {earlier}: \"{remainder}\" is both required and forbidden.",
                    "Keep one instruction and delete the other.");
            }

            seen.TryAdd((positive, remainder), line.Number);
        }
    }

    [GeneratedRegex(@"^(?:(?<neg>never|must not|do not)|(?<pos>always|must))\s+(?<rest>.+)$")]
    private static partial Regex ModalRegex();
}
