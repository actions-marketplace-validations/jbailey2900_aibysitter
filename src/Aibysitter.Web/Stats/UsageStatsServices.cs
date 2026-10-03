using Aibysitter.Web.Data;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aibysitter.Web.Stats;

public static class UsageStatsServices
{
    /// <summary>
    /// Counter, flusher and reader when <c>ConnectionStrings:Aibysitter</c> is set; otherwise null implementations.
    /// Uses the context factory registered by <see cref="ScoreHistoryServices.AddAibysitterScoreHistory"/>, which runs first.
    /// </summary>
    public static IServiceCollection AddAibysitterUsageStats(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(AibysitterDbContext.ConnectionStringName)))
        {
            services.AddSingleton<IUsageCounter, NullUsageCounter>();
            services.AddSingleton<IUsageStats, NullUsageStats>();
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<UsageCounter>();
        services.AddSingleton<IUsageCounter>(sp => sp.GetRequiredService<UsageCounter>());
        services.AddSingleton<IUsageStats, EfUsageStats>();
        services.AddHostedService<UsageStatsFlusher>();
        return services;
    }
}
