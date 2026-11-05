using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KanbanStudio.Api.Models;
using Xunit;

namespace KanbanStudio.Tests;

public class AuthTests
{
    [Fact]
    public async Task Login_Success_ReturnsUsernameAndStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var payload = new LoginRequest { Username = "user", Password = "password" };
        var response = await client.PostAsJsonAsync("/api/auth/login", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.Equal("user", result.Username);
        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401WithDetail()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var payload = new LoginRequest { Username = "user", Password = "wrongpassword" };
        var response = await client.PostAsJsonAsync("/api/auth/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid username or password", doc.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_NonExistentUser_Returns401WithDetail()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var payload = new LoginRequest { Username = "nonexistent", Password = "password" };
        var response = await client.PostAsJsonAsync("/api/auth/login", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid username or password", doc.GetProperty("detail").GetString());
    }
}
