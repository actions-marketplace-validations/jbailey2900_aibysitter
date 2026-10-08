using System.Net;
using Aibysitter.Packs;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Infrastructure;
using Aibysitter.Web.Notes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aibysitter.Rules.Tests;

[Trait("Category", "Catalog")]
public class CatalogStartupCheckTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task RealCatalogs_Pass()
    {
        await new CatalogStartupCheck(factory.Services, NullLogger<CatalogStartupCheck>.Instance).StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailingCatalog_LogsCritical_WithItsName_AndRethrows()
    {
        var services = new ServiceCollection()
            .AddSingleton(new LintEngine())
            .AddSingleton(_ => new PackCatalog())
            .AddSingleton<GalleryCatalog>()
            .AddSingleton<NoteCatalog>(_ => throw new InvalidOperationException("notes broken"))
            .BuildServiceProvider();
        var logger = new ListLogger();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalogStartupCheck(services, logger).StartAsync(CancellationToken.None));

        Assert.Equal("notes broken", ex.Message);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Equal("Catalog NoteCatalog failed to load", entry.Message);
        Assert.Same(ex, entry.Exception);
    }

    [Fact]
    public void FailingCatalog_HostDoesNotStart()
    {
        using var broken = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<PackCatalog>(_ => throw new InvalidOperationException("packs broken"))));

        // Program's catch logs the startup exception and the host ends; the factory then sees the disposed host.
        Assert.ThrowsAny<Exception>(() => broken.CreateClient());
    }

    [Fact]
    public void EveryCatalogType_IsChecked()
    {
        var catalogs = new[] { typeof(PackCatalog).Assembly, typeof(GalleryCatalog).Assembly }
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && t.Name.EndsWith("Catalog", StringComparison.Ordinal));

        Assert.All(catalogs, t => Assert.Contains(t, CatalogStartupCheck.Catalogs));
    }

    private sealed class ListLogger : ILogger<CatalogStartupCheck>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}

/// <summary>Non-Development host with every catalog throwing and the startup check removed.</summary>
[Trait("Category", "Catalog")]
public class ErrorPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Message = "An error occurred while processing your request.";

    private HttpClient BrokenCatalogsClient() => factory.WithWebHostBuilder(b => b
            .UseEnvironment("Staging")
            .ConfigureTestServices(s =>
            {
                s.Remove(s.Single(d => d.ImplementationType == typeof(CatalogStartupCheck)));
                foreach (var type in CatalogStartupCheck.Catalogs)
                {
                    s.AddSingleton(type, _ => throw new InvalidOperationException($"{type.Name} broken"));
                }
            }))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    [Fact]
    public async Task ErrorPage_Renders_WithNoLayout()
    {
        var response = await BrokenCatalogsClient().GetAsync("/Error");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(Message, html);
        Assert.DoesNotContain("class=\"site-header\"", html);
        Assert.DoesNotContain("rel=\"canonical\"", html);
    }

    [Fact]
    public async Task ErrorPage_UsesSiteStylesheet_NoInlineStyle()
    {
        var response = await BrokenCatalogsClient().GetAsync("/Error");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<link rel=\"stylesheet\" href=\"/css/site.css\" />", html);
        Assert.DoesNotContain("<style", html);
        Assert.DoesNotContain(" style=", html);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Contains("<meta name=\"robots\" content=\"noindex\" />", html);

        var css = await BrokenCatalogsClient().GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Contains("main :not(pre) > code", await css.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/Packs")]
    [InlineData("/Gallery")]
    [InlineData("/Notes")]
    [InlineData("/llms.txt")]
    [InlineData("/sitemap.xml")]
    [InlineData("/packs/registry.json")]
    public async Task CatalogPages_Return500_WithTheErrorPage(string path)
    {
        var response = await BrokenCatalogsClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(Message, await response.Content.ReadAsStringAsync());
    }
}
