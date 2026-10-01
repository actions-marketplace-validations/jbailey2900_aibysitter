namespace Aibysitter.Web.Infrastructure;

public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public List<string> KnownNetworks { get; set; } = [];
}

public sealed class LintRateLimitSettings
{
    public const string SectionName = "RateLimiting:Lint";

    public int PermitLimit { get; set; } = 20;

    public int WindowSeconds { get; set; } = 60;
}
