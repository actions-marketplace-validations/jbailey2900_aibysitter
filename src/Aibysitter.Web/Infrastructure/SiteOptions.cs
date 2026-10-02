namespace Aibysitter.Web.Infrastructure;

/// <summary>Public origin for absolute URLs (canonical, OpenGraph, sitemap, llms.txt). Config section <c>Site</c>.</summary>
public sealed class SiteOptions
{
    public const string Section = "Site";

    public string BaseUrl { get; init; } = "https://aibysitting.net";

    /// <summary>Absolute URL for a root-relative path.</summary>
    public string Url(string path) => BaseUrl.TrimEnd('/') + path;

    public static SiteOptions From(IConfiguration configuration)
    {
        var options = configuration.GetSection(Section).Get<SiteOptions>() ?? new SiteOptions();
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException($"{Section}:BaseUrl must be an https origin with no path.");
        }

        return options;
    }
}
