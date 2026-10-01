namespace Aibysitter.Rules.Rules;

/// <summary>A code fence opened and never closed. Everything after it renders as code and is skipped by other rules.</summary>
public sealed class UnclosedCodeFence : IRule
{
    public string Id => "R012";
    public string Title => "Unclosed code fence";
    public Severity Severity => Severity.Error;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.UnclosedFenceLine is { } line)
        {
            yield return new Finding(
                Id,
                line,
                $"Code fence opened on line {line} is never closed; the remaining {file.Lines.Count - line} line{(file.Lines.Count - line == 1 ? "" : "s")} render{(file.Lines.Count - line == 1 ? "s" : "")} as code.",
                "Close the fence with a matching ``` or ~~~ line.");
        }
    }
}
