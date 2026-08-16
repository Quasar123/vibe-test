using System.Net;
using System.Net.Http.Json;
using VibeTest.Server.Models.Requests;
using VibeTest.Tests.Integration;

namespace VibeTest.Tests.Integration.Api;

[Collection(PostgreSqlCollection.Name)]
public class AuthRateLimitApiTests(PostgreSqlTestFixture postgres)
{
    [Fact]
    public async Task Login_exceeding_rate_limit_returns_429_with_retry_after()
    {
        using var database = new PostgreSqlTestDb(postgres.ConnectionString);
        using var factory = new ApiWebApplicationFactory(database.ConnectionString, "RateLimitTesting");
        var client = factory.CreateClient();
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
