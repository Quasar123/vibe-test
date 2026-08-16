using Microsoft.EntityFrameworkCore;
using Npgsql;
using VibeTest.Server.Data;

namespace VibeTest.Tests.Integration;

public sealed class PostgreSqlTestDb : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databaseName;

    public PostgreSqlTestDb(string adminConnectionString)
    {
        _databaseName = "vt_" + Guid.NewGuid().ToString("N")[..16];

        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        adminBuilder.Database = "postgres";

        using (var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            adminConnection.Open();
            using var createCommand = adminConnection.CreateCommand();
            createCommand.CommandText = $"""CREATE DATABASE "{_databaseName}" """;
            createCommand.ExecuteNonQuery();
        }

        var dbBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = _databaseName
        };
        _connectionString = dbBuilder.ConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseVibeTestPostgreSql(_connectionString)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.Migrate();
    }

    public AppDbContext Db { get; }

    public string ConnectionString => _connectionString;

    public void Dispose()
    {
        Db.Dispose();

        var adminBuilder = new NpgsqlConnectionStringBuilder(_connectionString)
        {
            Database = "postgres"
        };

        using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
        adminConnection.Open();

        using (var terminateCommand = adminConnection.CreateCommand())
        {
            terminateCommand.CommandText =
                """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @databaseName AND pid <> pg_backend_pid()
                """;
            terminateCommand.Parameters.AddWithValue("databaseName", _databaseName);
            terminateCommand.ExecuteNonQuery();
        }

        using var dropCommand = adminConnection.CreateCommand();
        dropCommand.CommandText = $"""DROP DATABASE IF EXISTS "{_databaseName}" """;
        dropCommand.ExecuteNonQuery();
    }
}
