using KanbanStudio.Api.Data;
using KanbanStudio.Api.Models;
using KanbanStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KanbanStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KanbanController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly IBoardAuditLogger _auditLogger;
    private readonly ILogger<KanbanController> _logger;

    public KanbanController(
        DatabaseService db,
        IBoardAuditLogger auditLogger,
        ILogger<KanbanController> logger)
    {
        _db = db;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetBoard()
    {
        _logger.LogInformation("Retrieving Kanban board for user: {Username}", "user");
        var data = _db.GetBoardData("user");
        if (data == null)
        {
            _logger.LogWarning("Kanban board not found for user: {Username}", "user");
            return NotFound(new { detail = "Board not found" });
        }

        return Ok(data);
    }

    [HttpPut]
    public IActionResult UpdateBoard([FromBody] BoardData? payload)
    {
        if (payload == null || payload.Columns == null || payload.Cards == null)
        {
            _logger.LogWarning("Invalid board payload received for user: {Username}", "user");
            return UnprocessableEntity(new { detail = "Invalid board payload" });
        }

        var oldBoard = _db.GetBoardData("user");
        var success = _db.UpdateBoardData("user", payload);
        if (!success)
        {
            _logger.LogWarning("Failed to update board: user or board not found for {Username}", "user");
            return NotFound(new { detail = "Board or user not found" });
        }

        _auditLogger.LogBoardChanges(oldBoard, payload, "UserUpdate");
        _logger.LogInformation(
            "Kanban board updated successfully for user: {Username}. TotalColumns: {ColumnCount}, TotalCards: {CardCount}",
            "user",
            payload.Columns.Count,
            payload.Cards.Count
        );

        return Ok(payload);
    }
}
