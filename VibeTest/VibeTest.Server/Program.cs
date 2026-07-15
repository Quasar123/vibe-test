using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using VibeTest.Server;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var logsDir = Path.Combine(
            context.HostingEnvironment.ContentRootPath,
            context.Configuration["LogOutput:Directory"] ?? "logs");
        var destination = context.Configuration["LogOutput:Destination"] ?? "File";

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();

        switch (destination.Trim().ToLowerInvariant())
        {
            case "file":
                Directory.CreateDirectory(logsDir);
                configuration.WriteTo.File(
                    Path.Combine(logsDir, context.Configuration["LogOutput:FilePath"] ?? "vibetest-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true);
                break;
            case "sqlite":
                Directory.CreateDirectory(logsDir);
                configuration.WriteTo.SQLite(
                    sqliteDbPath: Path.Combine(logsDir, context.Configuration["LogOutput:SqlitePath"] ?? "vibetest-logs.db"),
                    tableName: context.Configuration["LogOutput:SqliteTableName"] ?? "Logs",
                    storeTimestampInUtc: true);
                break;
            case "both":
                Directory.CreateDirectory(logsDir);
                configuration.WriteTo.File(
                    Path.Combine(logsDir, context.Configuration["LogOutput:FilePath"] ?? "vibetest-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true);
                configuration.WriteTo.SQLite(
                    sqliteDbPath: Path.Combine(logsDir, context.Configuration["LogOutput:SqlitePath"] ?? "vibetest-logs.db"),
                    tableName: context.Configuration["LogOutput:SqliteTableName"] ?? "Logs",
                    storeTimestampInUtc: true);
                break;
            case "none":
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported LogOutput:Destination '{destination}'. Use File, SQLite, Both, or None.");
        }
    });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
    builder.Services.AddOpenApi();
    builder.Services.AddVibeTestServices(builder.Configuration);

    var app = builder.Build();
    app.UseVibeTestPipeline();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось с ошибкой");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
