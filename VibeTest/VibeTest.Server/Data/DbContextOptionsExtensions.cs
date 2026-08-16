using EFCore.NamingConventions;
using Microsoft.EntityFrameworkCore;

namespace VibeTest.Server.Data;

public static class DbContextOptionsExtensions
{
    public static DbContextOptionsBuilder UseVibeTestPostgreSql(
        this DbContextOptionsBuilder options,
        string connectionString)
    {
        options.UseNpgsql(connectionString);
        options.UseSnakeCaseNamingConvention();
        return options;
    }

    public static DbContextOptionsBuilder<AppDbContext> UseVibeTestPostgreSql(
        this DbContextOptionsBuilder<AppDbContext> options,
        string connectionString)
    {
        options.UseNpgsql(connectionString);
        options.UseSnakeCaseNamingConvention();
        return options;
    }
}
