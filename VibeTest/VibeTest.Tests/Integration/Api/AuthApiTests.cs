using System.Net;
using System.Net.Http.Json;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;
using VibeTest.Tests.Integration;

namespace VibeTest.Tests.Integration.Api;

[Collection(PostgreSqlCollection.Name)]
public class AuthApiTests(PostgreSqlTestFixture postgres)
{
    private readonly ApiWebApplicationFactory _factory = postgres.Factory;

    [Fact]
    public async Task Register_login_and_me_work()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "password123",
            DisplayName = "Alice"
        });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(_factory.JsonOptions);
        Assert.NotNull(auth?.AccessToken);
        Assert.Equal("Alice", auth.User.DisplayName);

        _factory.Authorize(client, auth.AccessToken);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.Content.ReadFromJsonAsync<UserDto>(_factory.JsonOptions);
        Assert.Equal(email, user!.Email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "password123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@test.com";
        await _factory.RegisterAndGetTokenAsync(client, email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Concurrent_refresh_with_same_token_returns_one_ok_and_one_unauthorized()
    {
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"concurrent-{Guid.NewGuid():N}@test.com",
            Password = "password123",
            DisplayName = "Concurrent"
        });

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(_factory.JsonOptions);
        var request = new RefreshTokenRequest { RefreshToken = auth!.RefreshToken };

        var first = client.PostAsJsonAsync("/api/auth/refresh", request);
        var second = client.PostAsJsonAsync("/api/auth/refresh", request);
        var responses = await Task.WhenAll(first, second);

        var statuses = responses.Select(r => r.StatusCode).ToArray();
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Unauthorized, statuses);
    }

    [Fact]
    public async Task Refresh_rotates_token()
    {
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"refresh-{Guid.NewGuid():N}@test.com",
            Password = "password123",
            DisplayName = "Bob"
        });

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(_factory.JsonOptions);
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = auth!.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var tokens = await refresh.Content.ReadFromJsonAsync<TokenRefreshResponse>(_factory.JsonOptions);
        Assert.NotEqual(auth.RefreshToken, tokens!.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
    }
}
