namespace Aibysitter.Rules;

/// <summary>
/// Version of everything that decides a lint score: R-rules on the lint page, format detection, and scoring.
/// Bump by hand on any change to those, and add a <see cref="RulesetChangelog"/> entry. App checks (P-checks, R006) do not bump it.
/// </summary>
public static class RulesetVersion
{
    public const int Current = 4;
}
