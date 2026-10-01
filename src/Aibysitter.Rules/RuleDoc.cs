namespace Aibysitter.Rules;

/// <param name="AppOnly">Runs only in the GitHub App (needs the repository), not on the lint page.</param>
public sealed record RuleDoc(string Id, string Name, string Summary, string Flags, string BadExample, string GoodExample, bool AppOnly = false);
