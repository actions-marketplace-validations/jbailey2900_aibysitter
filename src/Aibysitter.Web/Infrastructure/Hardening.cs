using System.Threading.RateLimiting;
using Aibysitter.Web.Linting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

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

        var badge = configuration.GetSection(BadgeRateLimitSettings.SectionName).Get<BadgeRateLimitSettings>()
            ?? new BadgeRateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteApiRejectionAsync;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsLintRequest(context.Request)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = lint.PermitLimit,
                            Window = TimeSpan.FromSeconds(lint.WindowSeconds),
                            QueueLimit = 0,
                        })
                    : IsBadgeRequest(context.Request)
                        ? RateLimitPartition.GetFixedWindowLimiter(
                            "badge|" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = badge.PermitLimit,
                                Window = TimeSpan.FromSeconds(badge.WindowSeconds),
                                QueueLimit = 0,
                            })
                        : RateLimitPartition.GetNoLimiter(string.Empty));
        });

        return services;
    }

    /// <summary>Form posts and API calls share one bucket per client IP.</summary>
    private static bool IsLintRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && (request.Path.Equals("/Lint", StringComparison.OrdinalIgnoreCase)
            || request.Path.Equals(LintApi.Path, StringComparison.OrdinalIgnoreCase));

    /// <summary>Badges: one bucket per client IP, separate from linting.</summary>
    private static bool IsBadgeRequest(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) && request.Path.StartsWithSegments(BadgeEndpoints.Prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>API callers get a ProblemDetails body and no-store; the form keeps the bare 429.</summary>
    private static ValueTask WriteApiRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        if (!context.HttpContext.Request.Path.Equals(LintApi.Path, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.CompletedTask;
        }

        response.Headers.CacheControl = "no-store";
        return new ValueTask(response.WriteAsJsonAsync(
            new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests. Try again in a minute." },
            options: null,
            contentType: "application/problem+json",
            cancellationToken));
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
