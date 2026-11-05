using System.Net;
using System.Net.Http.Json;
using KanbanStudio.Api.Models;
using KanbanStudio.Api.Services;
using Xunit;

namespace KanbanStudio.Tests;

public class ChatApiTests
{
    private class FakeChatService : IAIChatService
    {
        public Func<string, List<ChatMessage>, BoardData, Task<AIChatResponse>>? Handler { get; set; }

        public Task<AIChatResponse> ChatAsync(string message, List<ChatMessage> history, BoardData currentBoard, CancellationToken cancellationToken = default)
        {
            if (Handler != null)
            {
                return Handler(message, history, currentBoard);
            }
            return Task.FromResult(new AIChatResponse { Reply = "Default fake reply" });
        }
    }

    [Fact]
    public async Task Chat_Conversational_LeavesDbUnchanged()
    {
        var fakeService = new FakeChatService
        {
            Handler = (msg, history, board) => Task.FromResult(new AIChatResponse
            {
                Reply = "Your board looks great! You have 5 columns.",
                Board = null
            })
        };

        using var factory = new CustomWebApplicationFactory { MockChatService = fakeService };
        var client = factory.CreateClient();

        var payload = new AIChatRequest
        {
            Message = "How is my board doing?",
            History = new List<ChatMessage>()
        };

        var response = await client.PostAsJsonAsync("/api/chat", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<AIChatResponse>();
        Assert.NotNull(data);
        Assert.Equal("Your board looks great! You have 5 columns.", data.Reply);
        Assert.Null(data.Board);

        // Verify database board remains unchanged
        var boardResponse = await client.GetAsync("/api/kanban");
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardData>();
        Assert.NotNull(board);
        Assert.Equal(8, board.Cards.Count);
    }

    [Fact]
    public async Task Chat_BoardMutation_PersistsToDb()
    {
        var newCardId = "card-mock-1";
        var fakeService = new FakeChatService
        {
            Handler = (msg, history, board) =>
            {
                board.Cards[newCardId] = new Card
                {
                    Id = newCardId,
                    Title = "Created by AI",
                    Details = "Generated via mock"
                };
                board.Columns[0].CardIds.Add(newCardId);

                return Task.FromResult(new AIChatResponse
                {
                    Reply = "I've added the card 'Created by AI' to Backlog.",
                    Board = board
                });
            }
        };

        using var factory = new CustomWebApplicationFactory { MockChatService = fakeService };
        var client = factory.CreateClient();

        var payload = new AIChatRequest
        {
            Message = "Add a card titled 'Created by AI' to Backlog",
            History = new List<ChatMessage>()
        };

        var response = await client.PostAsJsonAsync("/api/chat", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<AIChatResponse>();
        Assert.NotNull(data);
        Assert.Contains("Created by AI", data.Reply);
        Assert.NotNull(data.Board);
        Assert.True(data.Board.Cards.ContainsKey(newCardId));

        // Verify persisted in SQLite
        var boardResponse = await client.GetAsync("/api/kanban");
        var persistedBoard = await boardResponse.Content.ReadFromJsonAsync<BoardData>();
        Assert.NotNull(persistedBoard);
        Assert.True(persistedBoard.Cards.ContainsKey(newCardId));
        Assert.Equal("Created by AI", persistedBoard.Cards[newCardId].Title);
        Assert.Contains(newCardId, persistedBoard.Columns[0].CardIds);
    }
}
