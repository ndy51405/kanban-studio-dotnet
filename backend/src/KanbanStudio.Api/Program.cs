using KanbanStudio.Api.Data;
using KanbanStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// Add services
builder.Services.AddHttpClient<IAIChatService, GeminiChatService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<IBoardAuditLogger, BoardAuditLogger>();

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

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

app.MapControllers();

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
