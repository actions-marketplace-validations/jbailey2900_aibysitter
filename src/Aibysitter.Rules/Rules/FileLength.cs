namespace Aibysitter.Rules.Rules;

public sealed class FileLength : IRule
{
    public const int MaxLines = 200;

    public string Id => "R004";
    public string Title => "File length";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Lines.Count > MaxLines)
        {
            yield return new Finding(
                Id,
                MaxLines + 1,
                $"File has {file.Lines.Count} lines; limit is {MaxLines}.",
                "Cut or split the file.");
        }
    }
}
