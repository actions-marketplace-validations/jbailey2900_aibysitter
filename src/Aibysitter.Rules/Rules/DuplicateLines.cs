namespace Aibysitter.Rules.Rules;

public sealed class DuplicateLines : IRule
{
    public string Id => "R005";
    public string Title => "Duplicate lines";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in file.Lines.Where(l => !l.IsBlank && !l.IsInCodeFence))
        {
            var key = TextNormalizer.Normalize(line.Text);
            if (key.Length == 0)
            {
                continue;
            }

            if (firstSeen.TryGetValue(key, out var first))
            {
                yield return new Finding(Id, line.Number, $"Duplicate of line {first}.", "Delete the repeated line.");
            }
            else
            {
                firstSeen[key] = line.Number;
            }
        }
    }
}
