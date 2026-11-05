using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KanbanStudio.Api.Models;
using Xunit;

namespace KanbanStudio.Tests;

public class KanbanApiTests
{
    [Fact]
    public void InitDb_CreatesFileAndSeedsDefaults()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.CreateClient();

        Assert.True(File.Exists(factory.DbPath));
        Assert.True(new FileInfo(factory.DbPath).Length > 0);
    }

    [Fact]
    public async Task GetKanban_ReturnsSeededColumnsAndCards()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/kanban");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var board = await response.Content.ReadFromJsonAsync<BoardData>();
        Assert.NotNull(board);
        Assert.Equal(5, board.Columns.Count);
        Assert.Equal(8, board.Cards.Count);

        var columnIds = board.Columns.Select(c => c.Id).ToList();
        Assert.Equal(new[] { "col-backlog", "col-discovery", "col-progress", "col-review", "col-done" }, columnIds);

        Assert.True(board.Cards.ContainsKey("card-1"));
        Assert.Equal("Align roadmap themes", board.Cards["card-1"].Title);
    }

    [Fact]
    public async Task PutKanban_PersistsMutationsInSqlite()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var initialResponse = await client.GetAsync("/api/kanban");
        var board = await initialResponse.Content.ReadFromJsonAsync<BoardData>();
        Assert.NotNull(board);

        // 1. Rename column
        board.Columns[0].Title = "Upcoming Ideas";

        // 2. Add new card
        board.Cards["card-99"] = new Card
        {
            Id = "card-99",
            Title = "New feature",
            Details = "Integration test details"
        };
        board.Columns[0].CardIds.Add("card-99");

        // 3. Edit existing card
        board.Cards["card-1"].Title = "Updated title";

        // 4. Move card across columns
        board.Columns[0].CardIds.Remove("card-2");
        board.Columns[1].CardIds.Add("card-2");

        // 5. Delete card
        board.Cards.Remove("card-8");
        board.Columns[4].CardIds.Remove("card-8");

        var putResponse = await client.PutAsJsonAsync("/api/kanban", board);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        // Fetch again and verify persistence in SQLite
        var verifyResponse = await client.GetAsync("/api/kanban");
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var updatedBoard = await verifyResponse.Content.ReadFromJsonAsync<BoardData>();
        Assert.NotNull(updatedBoard);

        Assert.Equal("Upcoming Ideas", updatedBoard.Columns[0].Title);
        Assert.True(updatedBoard.Cards.ContainsKey("card-99"));
        Assert.Equal("New feature", updatedBoard.Cards["card-99"].Title);
        Assert.Equal("Updated title", updatedBoard.Cards["card-1"].Title);
        Assert.Contains("card-2", updatedBoard.Columns[1].CardIds);
        Assert.False(updatedBoard.Cards.ContainsKey("card-8"));
        Assert.DoesNotContain("card-8", updatedBoard.Columns[4].CardIds);
    }

    [Fact]
    public async Task PutKanban_InvalidPayload_RejectedWith422()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Missing cards object
        var invalidBoard = new { columns = Array.Empty<object>() };
        var response = await client.PutAsJsonAsync("/api/kanban", invalidBoard);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
