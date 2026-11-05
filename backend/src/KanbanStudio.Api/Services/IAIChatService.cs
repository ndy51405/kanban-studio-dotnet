using KanbanStudio.Api.Models;

namespace KanbanStudio.Api.Services;

public interface IAIChatService
{
    Task<AIChatResponse> ChatAsync(string message, List<ChatMessage> history, BoardData currentBoard, CancellationToken cancellationToken = default);
}
