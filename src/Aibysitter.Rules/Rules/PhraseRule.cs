using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>Base for rules that flag prose lines containing listed phrases. One finding per line.</summary>
public abstract class PhraseRule : IRule
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract Severity Severity { get; }

    protected abstract Regex Pattern { get; }
    protected abstract string MessagePrefix { get; }
    protected abstract string FixHint { get; }

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        foreach (var line in file.Lines.Where(l => l.IsProse))
        {
            var matches = Pattern.Matches(line.Text)
                .Select(m => m.Value.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (matches.Count > 0)
            {
                yield return new Finding(Id, line.Number, $"{MessagePrefix}: {string.Join(", ", matches.Select(m => $"\"{m}\""))}", FixHint);
            }
        }
    }
}
