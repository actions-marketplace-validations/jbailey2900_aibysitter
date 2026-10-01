namespace Aibysitter.Rules.Rules;

/// <summary>Credential-like values on any line, including code blocks.</summary>
public sealed class SecretsInRulesFile : IRule
{
    public string Id => "R009";
    public string Title => "Secrets in rules file";
    public Severity Severity => Severity.Error;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        foreach (var line in file.Lines.Where(l => !l.IsBlank))
        {
            var matches = SecretPatterns.Find(line.Text);
            if (matches.Count > 0)
            {
                yield return new Finding(
                    Id,
                    line.Number,
                    $"Possible secret: {string.Join(", ", matches.Select(m => $"{m.Kind} ({m.Redacted})"))}",
                    "Remove the value and rotate it. Reference an environment variable or secret store by name.");
            }
        }
    }
}
