using System.Net;
using VibeTest.Tests.Integration;

namespace VibeTest.Tests.Integration.Api;

[Collection(PostgreSqlCollection.Name)]
public class HealthEndpointApiTests(PostgreSqlTestFixture postgres)
{
    private readonly ApiWebApplicationFactory _factory = postgres.Factory;

    [Fact]
    public async Task Health_live_returns_ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ready_returns_ok_when_database_is_available()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
