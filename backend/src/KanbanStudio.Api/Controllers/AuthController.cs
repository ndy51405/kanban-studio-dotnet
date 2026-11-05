using KanbanStudio.Api.Data;
using KanbanStudio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace KanbanStudio.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _db;

    public AuthController(DatabaseService db)
    {
        _db = db;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Username) || !_db.AuthenticateUser(payload.Username, payload.Password))
        {
            return StatusCode(401, new { detail = "Invalid username or password" });
        }

        return Ok(new LoginResponse { Username = payload.Username, Status = "ok" });
    }
}

