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

    public ChatController(DatabaseService db, IAIChatService chatService)
    {
        _db = db;
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] AIChatRequest payload, CancellationToken ct)
    {
        var board = _db.GetBoardData("user");
        if (board == null)
        {
            return NotFound(new { detail = "Board not found" });
        }

        try
        {
            var response = await _chatService.ChatAsync(payload.Message, payload.History, board, ct);
            if (response.Board != null)
            {
                _db.UpdateBoardData("user", response.Board);
            }
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"AI service error: {ex.Message}" });
        }
    }
}

