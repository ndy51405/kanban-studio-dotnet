using KanbanStudio.Api.Data;
using KanbanStudio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace KanbanStudio.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(DatabaseService db, ILogger<AuthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Username) || !_db.AuthenticateUser(payload.Username, payload.Password))
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", payload?.Username);
            return StatusCode(401, new { detail = "Invalid username or password" });
        }

        _logger.LogInformation("User logged in successfully: {Username}", payload.Username);
        return Ok(new LoginResponse { Username = payload.Username, Status = "ok" });
    }
}
