using KanbanStudio.Api.Data;
using KanbanStudio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace KanbanStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KanbanController : ControllerBase
{
    private readonly DatabaseService _db;

    public KanbanController(DatabaseService db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult GetBoard()
    {
        var data = _db.GetBoardData("user");
        if (data == null)
        {
            return NotFound(new { detail = "Board not found" });
        }

        return Ok(data);
    }

    [HttpPut]
    public IActionResult UpdateBoard([FromBody] BoardData? payload)
    {
        if (payload == null || payload.Columns == null || payload.Cards == null)
        {
            return UnprocessableEntity(new { detail = "Invalid board payload" });
        }

        var success = _db.UpdateBoardData("user", payload);
        if (!success)
        {
            return NotFound(new { detail = "Board or user not found" });
        }

        return Ok(payload);
    }
}

