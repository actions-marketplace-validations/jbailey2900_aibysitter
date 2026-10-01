using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace Aibysitter.Web.Infrastructure;

public static class Hardening
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

    public static IServiceCollection AddAibysitterHardening(this IServiceCollection services, IConfiguration configuration)
    {
        var forwarded = configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var cidr in forwarded.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
            }
        });

        var lint = configuration.GetSection(LintRateLimitSettings.SectionName).Get<LintRateLimitSettings>()
            ?? new LintRateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                HttpMethods.IsPost(context.Request.Method)
                && context.Request.Path.Equals("/Lint", StringComparison.OrdinalIgnoreCase)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = lint.PermitLimit,
                            Window = TimeSpan.FromSeconds(lint.WindowSeconds),
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter(string.Empty));
        });

        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                return Task.CompletedTask;
            });

            await next(context);
        });
}
