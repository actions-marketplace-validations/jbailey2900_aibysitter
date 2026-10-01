namespace Aibysitter.Web.Samples;

public static class SampleRules
{
    private const string ResourceName = "Aibysitter.Web.Samples.sample-rules.md";

    private static readonly Lazy<string> LazyText = new(() =>
    {
        using var stream = typeof(SampleRules).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static string Text => LazyText.Value;
}
