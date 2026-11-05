using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace KanbanStudio.Tests;

public class HealthTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal("Hello from ASP.NET Core", doc.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Root_Serves_KanbanApp()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Kanban Studio", content);
    }

    [Fact]
    public async Task StaticAssets_AreServed()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");
        var match = Regex.Match(html, @"src=""(/assets/[^""]+)""");
        Assert.True(match.Success, "Expected to find /assets/... script tag in index.html");

        var assetPath = match.Groups[1].Value;
        var assetResponse = await client.GetAsync(assetPath);
        Assert.Equal(System.Net.HttpStatusCode.OK, assetResponse.StatusCode);
    }
}
