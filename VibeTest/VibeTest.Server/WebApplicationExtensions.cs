using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using VibeTest.Server.Configuration;
using VibeTest.Server.Data;
using VibeTest.Server.Middleware;

namespace VibeTest.Server;

public static class WebApplicationExtensions
{
    public static WebApplication UseVibeTestPipeline(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (app.Environment.IsEnvironment("Testing"))
                db.Database.EnsureCreated();
            else
                db.Database.Migrate();
        }

        app.Use(async (context, next) =>
        {
            using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
            {
                await next(context);
            }
        });

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress?.ToString());
                diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
            };
        });
        app.UseMiddleware<DomainExceptionMiddleware>();

        if (!app.Environment.IsEnvironment("E2E") && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseCors("Spa");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapControllers();

        if (app.Environment.IsDevelopment())
        {
            app.UseDefaultFiles();
            app.MapStaticAssets();
            app.MapFallbackToFile("/index.html");
        }

        return app;
    }
}
