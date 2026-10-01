using System.Text;

namespace Aibysitter.Web.Gallery;

public static class GalleryEndpoints
{
    public static IEndpointRouteBuilder MapGalleryDownloads(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/gallery/{id}/{file}", (string id, string file, GalleryCatalog catalog) =>
            catalog.Find(id) is { } entry && string.Equals(entry.FileName, file, StringComparison.Ordinal)
                ? Results.File(Encoding.UTF8.GetBytes(entry.Content), "text/markdown; charset=utf-8", entry.FileName)
                : Results.NotFound());
        return endpoints;
    }
}
