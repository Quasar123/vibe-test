using System.Net;

namespace VibeTest.Tests.Integration.Api;

public class HealthEndpointApiTests : IClassFixture<ApiFixture>
{
    private readonly ApiWebApplicationFactory _factory;

    public HealthEndpointApiTests(ApiFixture fixture) => _factory = fixture.Factory;

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
