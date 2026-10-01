namespace Aibysitter.Rules;

public sealed record Finding(string RuleId, int Line, string Message, string FixHint);
