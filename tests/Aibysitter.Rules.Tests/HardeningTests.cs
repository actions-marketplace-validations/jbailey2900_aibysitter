using System.Net;
using Aibysitter.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aibysitter.Rules.Tests;

public class HardeningTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestRemoteIpHeader = "X-Test-Remote-IP";
    private const string CloudflareIp = "104.16.0.1";
    private const string NonCloudflareIp = "198.51.100.7";

    [Fact]
    public async Task Responses_IncludeSecurityHeaders()
    {
        var response = await factory.CreateClient().GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal(Hardening.ContentSecurityPolicy, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task LintPost_OverLimit_Returns429_AndGetIsNotLimited()
    {
        var client = CreateClient(permitLimit: 2);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostLint(client, NonCloudflareIp)).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostLint(client, NonCloudflareIp)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostLint(client, NonCloudflareIp)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Lint")).StatusCode);
    }

    [Fact]
    public async Task ForwardedFor_FromCloudflare_PartitionsByClient()
    {
        var client = CreateClient(permitLimit: 1);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostLint(client, CloudflareIp, "203.0.113.1")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostLint(client, CloudflareIp, "203.0.113.2")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostLint(client, CloudflareIp, "203.0.113.1")).StatusCode);
    }

    [Fact]
    public async Task ForwardedFor_FromUnknownProxy_IsIgnored()
    {
        var client = CreateClient(permitLimit: 1);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostLint(client, NonCloudflareIp, "203.0.113.1")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostLint(client, NonCloudflareIp, "203.0.113.2")).StatusCode);
    }

    [Theory]
    [InlineData("aibysitting.net", HttpStatusCode.OK)]
    [InlineData("www.aibysitting.net", HttpStatusCode.OK)]
    [InlineData("evil.example", HttpStatusCode.BadRequest)]
    public async Task Production_FiltersHosts(string host, HttpStatusCode expected)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "aibysitter-tests", "log-.txt");
        var client = factory
            .WithWebHostBuilder(b => b
                .UseEnvironment("Production")
                .UseSetting("Serilog:WriteTo:0:Args:path", logPath))
            .CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = host;

        Assert.Equal(expected, (await client.SendAsync(request)).StatusCode);
    }

    private HttpClient CreateClient(int permitLimit) =>
        factory
            .WithWebHostBuilder(b => b
                .UseSetting("RateLimiting:Lint:PermitLimit", permitLimit.ToString())
                .ConfigureServices(s => s.AddTransient<IStartupFilter, TestRemoteIpStartupFilter>()))
            .CreateClient();

    private static Task<HttpResponseMessage> PostLint(HttpClient client, string remoteIp, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/Lint")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["RulesText"] = "x" }),
        };
        request.Headers.Add(TestRemoteIpHeader, remoteIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request);
    }

    private sealed class TestRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(TestRemoteIpHeader, out var ip))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                }

                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
