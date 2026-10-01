namespace Aibysitter.Rules;

public interface IRule
{
    string Id { get; }
    string Title { get; }
    Severity Severity { get; }
    IEnumerable<Finding> Evaluate(RulesFile file);
}
