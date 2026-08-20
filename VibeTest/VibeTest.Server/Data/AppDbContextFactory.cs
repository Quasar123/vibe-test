using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VibeTest.Server.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=vibetest;Username=vibetest;Password=changeme";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseVibeTestPostgreSql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
