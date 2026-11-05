using KanbanStudio.Api.Data;
using KanbanStudio.Api.Models;
using KanbanStudio.Api.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddHttpClient<IAIChatService, GeminiChatService>();
builder.Services.AddSingleton<DatabaseService>();

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize database
var db = app.Services.GetRequiredService<DatabaseService>();
db.InitDb();

app.UseCors();

// API Endpoints
app.MapGet("/api/health", () =>
{
    return Results.Ok(new { status = "ok", message = "Hello from ASP.NET Core" });
});

app.MapPost("/api/auth/login", (LoginRequest payload, DatabaseService db) =>
{
    if (string.IsNullOrWhiteSpace(payload.Username) || !db.AuthenticateUser(payload.Username, payload.Password))
    {
        return Results.Json(new { detail = "Invalid username or password" }, statusCode: 401);
    }
    return Results.Ok(new LoginResponse { Username = payload.Username, Status = "ok" });
});

app.MapGet("/api/kanban", (DatabaseService db) =>
{
    var data = db.GetBoardData("user");
    if (data == null)
    {
        return Results.Json(new { detail = "Board not found" }, statusCode: 404);
    }
    return Results.Ok(data);
});

app.MapPut("/api/kanban", (BoardData? payload, DatabaseService db) =>
{
    if (payload == null || payload.Columns == null || payload.Cards == null)
    {
        return Results.Json(new { detail = "Invalid board payload" }, statusCode: 422);
    }

    var success = db.UpdateBoardData("user", payload);
    if (!success)
    {
        return Results.Json(new { detail = "Board or user not found" }, statusCode: 404);
    }
    return Results.Ok(payload);
});

app.MapPost("/api/chat", async (AIChatRequest payload, DatabaseService db, IAIChatService chatService, CancellationToken ct) =>
{
    var board = db.GetBoardData("user");
    if (board == null)
    {
        return Results.Json(new { detail = "Board not found" }, statusCode: 404);
    }

    try
    {
        var response = await chatService.ChatAsync(payload.Message, payload.History, board, ct);
        if (response.Board != null)
        {
            db.UpdateBoardData("user", response.Board);
        }
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Json(new { detail = $"AI service error: {ex.Message}" }, statusCode: 500);
    }
});

// Serve frontend static files
var staticDir = ResolveStaticDirectory();
if (Directory.Exists(staticDir))
{
    var fileProvider = new PhysicalFileProvider(staticDir);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

    // Fallback for SPA routing, avoiding /api routes
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = 404;
            return;
        }

        var indexPath = Path.Combine(staticDir, "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexPath);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    });
}

app.Run();

static string ResolveStaticDirectory()
{
    var envStatic = Environment.GetEnvironmentVariable("STATIC_DIR");
    if (!string.IsNullOrWhiteSpace(envStatic) && Directory.Exists(envStatic))
    {
        return envStatic;
    }

    var inAppDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    if (Directory.Exists(inAppDir))
    {
        return inAppDir;
    }

    var baseDir = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    if (Directory.Exists(baseDir))
    {
        return baseDir;
    }

    foreach (var startDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var current = new DirectoryInfo(startDir);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "dist");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }
    }

    return inAppDir;
}

public partial class Program { }
