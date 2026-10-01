namespace Aibysitter.Rules.Tests;

internal static class Fixtures
{
    public static RulesFile Load(string name) =>
        RulesFile.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name)));
}
