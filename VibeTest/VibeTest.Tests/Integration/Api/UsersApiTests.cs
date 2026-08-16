using System.Net;
using System.Net.Http.Json;
using VibeTest.Server.Models.Requests;
using VibeTest.Server.Models.Responses;
using VibeTest.Tests.Integration;

namespace VibeTest.Tests.Integration.Api;

[Collection(PostgreSqlCollection.Name)]
public class UsersApiTests(PostgreSqlTestFixture postgres)
{
    private readonly ApiWebApplicationFactory _factory = postgres.Factory;

    [Fact]
    public async Task Search_requires_auth()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/search?q=ab");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_validates_min_length()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterUserAsync(client, $"min-{Guid.NewGuid():N}@test.com", "Min User");
        _factory.Authorize(client, auth.AccessToken);

        var response = await client.GetAsync("/api/users/search?q=a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_excludes_current_user()
    {
        var client = _factory.CreateClient();
        var uniqueName = $"Self Search {Guid.NewGuid():N}";
        var auth = await RegisterUserAsync(client, $"self-{Guid.NewGuid():N}@test.com", uniqueName);
        _factory.Authorize(client, auth.AccessToken);

        var response = await client.GetAsync($"/api/users/search?q={Uri.EscapeDataString(uniqueName)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserSearchResult>>(_factory.JsonOptions);

        Assert.DoesNotContain(users!, user => user.Id == auth.User.Id);
    }

    [Fact]
    public async Task Search_clamps_limit()
    {
        var client = _factory.CreateClient();
        var auth = await RegisterUserAsync(client, $"limit-owner-{Guid.NewGuid():N}@test.com", "Limit Owner");
        _factory.Authorize(client, auth.AccessToken);

        var prefix = $"ClampLimit{Guid.NewGuid():N}";
        for (var i = 0; i < 25; i++)
        {
            await RegisterUserAsync(_factory.CreateClient(), $"limit-{Guid.NewGuid():N}@test.com", $"{prefix} {i:00}");
        }

        var response = await client.GetAsync($"/api/users/search?q={prefix}&limit=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserSearchResult>>(_factory.JsonOptions);

        Assert.NotNull(users);
        Assert.Equal(20, users.Count);
    }

    private async Task<AuthResponse> RegisterUserAsync(HttpClient client, string email, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "password123",
            DisplayName = displayName
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(_factory.JsonOptions))!;
    }
}
