using System.Net;
using System.Text.RegularExpressions;
using Aibysitter.Web.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public partial class DataProtectionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Host(string keysPath) =>
        factory
            .WithWebHostBuilder(b => b.UseSetting(DataProtectionSetup.KeysPathKey, keysPath))
            .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    /// <summary>GET /Lint on one host, POST the form to another (simulates an app pool recycle).</summary>
    private static async Task<HttpStatusCode> PostAcrossHosts(HttpClient first, HttpClient second)
    {
        var get = await first.GetAsync("/Lint");
        var html = await get.Content.ReadAsStringAsync();
        var token = TokenRegex().Match(html).Groups[1].Value;
        var cookie = string.Join("; ", get.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Lint")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["RulesText"] = "- Use tabs.",
            }),
        };
        request.Headers.Add("Cookie", cookie);

        return (await second.SendAsync(request)).StatusCode;
    }

    private static string TempDir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"aibysitter-dp-{Guid.NewGuid():N}")).FullName;

    [Fact]
    public async Task SharedKeysPath_FormFromOneHost_ValidatesOnAnother()
    {
        var keys = TempDir();
        try
        {
            Assert.Equal(HttpStatusCode.OK, await PostAcrossHosts(Host(keys), Host(keys)));
            Assert.NotEmpty(Directory.GetFiles(keys, "key-*.xml"));
        }
        finally
        {
            Directory.Delete(keys, recursive: true);
        }
    }

    [Fact]
    public async Task DifferentKeysPaths_FormFromOneHost_IsRejectedByAnother()
    {
        var a = TempDir();
        var b = TempDir();
        try
        {
            Assert.Equal(HttpStatusCode.BadRequest, await PostAcrossHosts(Host(a), Host(b)));
        }
        finally
        {
            Directory.Delete(a, recursive: true);
            Directory.Delete(b, recursive: true);
        }
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenRegex();
}
