using Aibysitter.Packs;
using Aibysitter.Rules;
using Aibysitter.Web.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Aibysitter.Web.RulesPacks;

/// <summary><c>/packs/registry.json</c>, schema version 1: packs, their sections and lint scores.</summary>
public static class PackRegistryEndpoints
{
    public const string Path = "/packs/registry.json";

    public static IEndpointRouteBuilder MapPackRegistry(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, (HttpContext http, PackScores scores, SiteOptions site) =>
        {
            http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=300";
            return Results.Json(new
            {
                schemaVersion = PackCatalog.SchemaVersion,
                rulesetVersion = RulesetVersion.Current,
                packs = scores.All.Select(v => new
                {
                    id = v.Pack.Id,
                    title = v.Pack.Manifest.Title,
                    description = v.Pack.Manifest.Description,
                    tags = v.Pack.Manifest.Tags,
                    targets = v.Pack.Manifest.Targets,
                    license = v.Pack.Manifest.License,
                    standalone = v.Pack.Manifest.Standalone,
                    score = v.Score.Value,
                    grade = v.Score.Grade,
                    pageUrl = site.Url($"/Packs#{v.Pack.Id}"),
                    init = v.InitCommand,
                    sections = v.Sections.Select(s => new
                    {
                        id = s.Section.Id,
                        heading = s.Section.Heading,
                        score = s.Score.Value,
                        grade = s.Score.Grade,
                    }),
                }),
            });
        });

        return endpoints;
    }
}
