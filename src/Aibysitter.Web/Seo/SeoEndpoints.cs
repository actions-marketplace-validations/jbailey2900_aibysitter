using System.Globalization;
using System.Text;
using System.Xml;
using Aibysitter.Web.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Aibysitter.Web.Seo;

/// <summary><c>/sitemap.xml</c>, <c>/robots.txt</c>, <c>/llms.txt</c>.</summary>
public static class SeoEndpoints
{
    public const string CacheControl = "public, max-age=3600";

    /// <summary>Paths that are not pages; listed under Disallow.</summary>
    public static readonly IReadOnlyList<string> Disallowed = ["/api/", "/github/webhook", "/health"];

    public static IEndpointRouteBuilder MapSeo(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/sitemap.xml", (HttpContext context, SiteMap map, SiteOptions site) =>
            Text(context, SiteMapXml(map, site), "application/xml; charset=utf-8"));
        endpoints.MapGet("/robots.txt", (HttpContext context, SiteOptions site) =>
            Text(context, RobotsTxt(site), "text/plain; charset=utf-8"));
        endpoints.MapGet("/llms.txt", (HttpContext context, SiteOptions site) =>
            Text(context, LlmsTxt.Build(site), "text/plain; charset=utf-8"));
        return endpoints;
    }

    public static string SiteMapXml(SiteMap map, SiteOptions site)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, NewLineChars = "\n", Encoding = new UTF8Encoding(false) };
        using (var writer = XmlWriter.Create(new StringWriter(builder), settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
            foreach (var entry in map.Entries)
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", site.Url(entry.Path));
                if (entry.LastModified is { } date)
                {
                    writer.WriteElementString("lastmod", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return builder.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"") + "\n";
    }

    public static string RobotsTxt(SiteOptions site) =>
        "User-agent: *\nAllow: /\n" + string.Concat(Disallowed.Select(p => $"Disallow: {p}\n")) + $"\nSitemap: {site.Url("/sitemap.xml")}\n";

    private static IResult Text(HttpContext context, string body, string contentType)
    {
        context.Response.Headers[HeaderNames.CacheControl] = CacheControl;
        return Results.Text(body, contentType, Encoding.UTF8);
    }
}
