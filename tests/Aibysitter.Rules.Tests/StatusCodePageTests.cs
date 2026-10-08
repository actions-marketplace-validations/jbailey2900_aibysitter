using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aibysitter.Rules.Tests;

public class StatusCodePageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/no-such-page")]
    [InlineData("/Gallery/does-not-exist")]
    [InlineData("/Rules/R099")]
    [InlineData("/Incidents/999")]
    public async Task NotFound_RendersSitePage_Keeps404(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<h1>Page not found.</h1>", html);
        Assert.Contains("class=\"site-header\"", html);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\" />", html);
        Assert.DoesNotContain("rel=\"canonical\"", html);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task OtherStatus_ShowsCodeAndReason()
    {
        var client = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, EmptyStatusFilter>())).CreateClient();

        var response = await client.GetAsync("/test-empty-410");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Contains("<h1>HTTP 410. Gone</h1>", html);
        Assert.DoesNotContain("Nothing is at this address.", html);
    }

    /// <summary>Answers /test-empty-410 with an empty 410, after the app's status code pages middleware.</summary>
    private sealed class EmptyStatusFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test-empty-410")
                {
                    context.Response.StatusCode = StatusCodes.Status410Gone;
                    return Task.CompletedTask;
                }

                return nextMiddleware(context);
            });
        };
    }

    [Fact]
    public async Task DirectRequest_Is404Page()
    {
        var response = await factory.CreateClient().GetAsync("/StatusCode/500");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Page not found.</h1>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Head_KeepsStatus()
    {
        var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Head, "/no-such-page"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BodiedResponses_AreUnchanged()
    {
        var client = factory.CreateClient();

        var badge = await client.GetAsync("/badge/-bad-/x.svg");
        Assert.Equal(HttpStatusCode.NotFound, badge.StatusCode);
        Assert.Equal("image/svg+xml", badge.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_EmptyErrorBodies_AreUnchanged()
    {
        var client = factory.CreateClient();

        var webhook = await client.PostAsync("/github/webhook", new StringContent("{}"));
        Assert.Empty(await webhook.Content.ReadAsStringAsync());

        var api = await client.PostAsync("/api/lint", new StringContent("x", System.Text.Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, api.StatusCode);
        Assert.Equal("application/problem+json", api.Content.Headers.ContentType?.MediaType);
    }
}
