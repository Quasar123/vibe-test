using System.Net;
using System.Net.Http.Json;
using VibeTest.Server.Models.Requests;

namespace VibeTest.Tests.Integration.Api;

public class AuthRateLimitApiTests : IClassFixture<ApiFixture>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthRateLimitApiTests(ApiFixture fixture) => _factory = fixture.Factory;

    [Fact]
    public async Task Login_exceeding_rate_limit_returns_429_with_retry_after()
    {
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest
        {
            Email = "rate-limit@test.com",
            Password = "wrong-password"
        };

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }
}
