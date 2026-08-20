using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using VibeTest.Server.Configuration;

namespace VibeTest.Server;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddVibeTestRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiterOptions.OnRejected = (context, _) =>
            {
                var httpContext = context.HttpContext;
                var partitionKey = GetPartitionKey(httpContext);
                var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("VibeTest.RateLimiting");

                logger.LogWarning(
                    "Rate limit exceeded: {Method} {Path} partition={PartitionKey}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    partitionKey);

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            if (!options.Enabled)
            {
                limiterOptions.AddPolicy(RateLimitPolicies.GlobalApi, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                limiterOptions.AddPolicy(RateLimitPolicies.AuthLogin, _ => RateLimitPartition.GetNoLimiter(string.Empty));
                limiterOptions.AddPolicy(RateLimitPolicies.AuthRegisterRefresh, _ => RateLimitPartition.GetNoLimiter(string.Empty));
            }
            else
            {
                limiterOptions.AddPolicy(RateLimitPolicies.GlobalApi, httpContext =>
                    CreateFixedWindowPartition(httpContext, options.Global));

                limiterOptions.AddPolicy(RateLimitPolicies.AuthLogin, httpContext =>
                    CreateFixedWindowPartition(httpContext, options.AuthLogin));

                limiterOptions.AddPolicy(RateLimitPolicies.AuthRegisterRefresh, httpContext =>
                    CreateFixedWindowPartition(httpContext, options.AuthRegisterRefresh));
            }
        });

        return services;
    }

    private static RateLimitPartition<string> CreateFixedWindowPartition(
        HttpContext httpContext,
        RateLimitPolicyOptions policyOptions) =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policyOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(policyOptions.WindowSeconds),
                QueueLimit = 0
            });

    internal static string GetPartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
