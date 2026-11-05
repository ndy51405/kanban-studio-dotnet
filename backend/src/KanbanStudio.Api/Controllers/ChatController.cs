using KanbanStudio.Api.Data;
using KanbanStudio.Api.Models;
using KanbanStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KanbanStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly IAIChatService _chatService;
    private readonly IBoardAuditLogger _auditLogger;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        DatabaseService db,
        IAIChatService chatService,
        IBoardAuditLogger auditLogger,
        ILogger<ChatController> logger)
    {
        _db = db;
        _chatService = chatService;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] AIChatRequest? payload, CancellationToken ct)
    {
        if (payload == null)
        {
            _logger.LogWarning("AI chat payload is null");
            return BadRequest(new { detail = "Payload cannot be null" });
        }

        _logger.LogInformation(
            "AI chat prompt received: {UserPrompt}. HistoryCount: {HistoryCount}",
            payload.Message,
            payload.History?.Count ?? 0
        );

        var board = _db.GetBoardData("user");
        if (board == null)
        {
            _logger.LogWarning("Board not found for user: {Username}", "user");
            return NotFound(new { detail = "Board not found" });
        }

        try
        {
            var response = await _chatService.ChatAsync(payload.Message, payload.History ?? new(), board, ct);
            if (response.Board != null)
            {
                _auditLogger.LogBoardChanges(board, response.Board, "AIChat");
                _db.UpdateBoardData("user", response.Board);
                _logger.LogInformation("AI chat updated the Kanban board for user: {Username}", "user");
            }

            _logger.LogInformation("AI chat response successfully processed for prompt: {UserPrompt}", payload.Message);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service error while processing prompt: {UserPrompt}", payload.Message);
            return StatusCode(500, new { detail = $"AI service error: {ex.Message}" });
        }
    }
}
