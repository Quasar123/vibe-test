using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VibeTest.Server.Configuration;
using VibeTest.Server.Data;
using VibeTest.Server.Data.Repositories;
using VibeTest.Server.Models.Entities;
using VibeTest.Server.Services;

namespace VibeTest.Server;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVibeTestServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

            options.UseVibeTestPostgreSql(connectionString);

            if (sp.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<ITestRepository, TestRepository>();
        services.AddScoped<IResultRepository, ResultRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITestService, TestService>();
        services.AddScoped<IResultService, ResultService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IUserService, UserService>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is missing.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(options =>
        {
            options.AddPolicy("Spa", policy =>
            {
                if (corsOptions.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        services.AddVibeTestRateLimiting(configuration);

        return services;
    }
}
