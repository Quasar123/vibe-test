using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using VibeTest.Tests.Integration.Api;
using Xunit;

namespace VibeTest.Tests.Integration;

public sealed class PostgreSqlTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private PostgreSqlTestDb? _apiDatabase;
    private bool _ownsContainer;

    public string ConnectionString { get; private set; } = string.Empty;

    public ApiWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var externalConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(externalConnection))
        {
            ConnectionString = externalConnection;
        }
        else
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("vibetest_tests")
                .WithUsername("vibetest")
                .WithPassword("changeme")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            _ownsContainer = true;
        }

        _apiDatabase = new PostgreSqlTestDb(ConnectionString);
        Factory = new ApiWebApplicationFactory(_apiDatabase.ConnectionString);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VibeTest.Server.Data.AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        _apiDatabase?.Dispose();

        if (_ownsContainer && _container is not null)
            await _container.DisposeAsync();
    }
}

[CollectionDefinition(PostgreSqlCollection.Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlTestFixture>
{
    public const string Name = "PostgreSql";
}
