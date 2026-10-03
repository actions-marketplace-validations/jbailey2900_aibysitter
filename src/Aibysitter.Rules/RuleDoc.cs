namespace Aibysitter.Rules;

/// <param name="AppOnly">Runs only in the GitHub App (needs the repository), not on the lint page.</param>
/// <param name="KnownLimits">Cases the rule gets wrong on purpose, shown on its rule page.</param>
public sealed record RuleDoc(string Id, string Name, string Summary, string Flags, string BadExample, string GoodExample, bool AppOnly = false, IReadOnlyList<string>? KnownLimits = null);
