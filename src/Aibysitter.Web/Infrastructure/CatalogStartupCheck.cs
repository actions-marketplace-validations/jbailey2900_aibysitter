using Aibysitter.Packs;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Incidents;
using Aibysitter.Web.Notes;
using Aibysitter.Web.RulesPacks;
using Aibysitter.Web.Seo;

namespace Aibysitter.Web.Infrastructure;

/// <summary>
/// Loads every embedded-content catalog before the server takes requests. A failure is logged and rethrown, so the
/// host does not start and the deploy health check fails.
/// </summary>
public sealed class CatalogStartupCheck(IServiceProvider services, ILogger<CatalogStartupCheck> logger) : IHostedService
{
    public static readonly IReadOnlyList<Type> Catalogs =
        [typeof(PackCatalog), typeof(GalleryCatalog), typeof(NoteCatalog), typeof(IncidentCatalog), typeof(PackScores), typeof(SiteMap)];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var type in Catalogs)
        {
            try
            {
                Load(services.GetRequiredService(type));
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Catalog {Catalog} failed to load", type.Name);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>PackCatalog, NoteCatalog and IncidentCatalog load in their constructors; the rest load on first access.</summary>
    private static void Load(object catalog)
    {
        _ = catalog switch
        {
            GalleryCatalog gallery => gallery.All.Count,
            PackScores scores => scores.All.Count,
            SiteMap map => map.Entries.Count,
            _ => 0,
        };
    }
}
