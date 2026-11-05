using KanbanStudio.Api.Data;
using KanbanStudio.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanStudio.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    public IAIChatService? MockChatService { get; set; }

    public CustomWebApplicationFactory(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(Path.GetTempPath(), $"test_pm_{Guid.NewGuid()}.db");
    }

    public string DbPath => _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DatabaseService));
            if (dbDescriptor != null)
            {
                services.Remove(dbDescriptor);
            }

            var dbService = new DatabaseService(_dbPath);
            dbService.InitDb();
            services.AddSingleton(dbService);

            if (MockChatService != null)
            {
                var chatDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIChatService));
                if (chatDescriptor != null)
                {
                    services.Remove(chatDescriptor);
                }
                services.AddSingleton(MockChatService);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup failures in test temp files
        }
    }
}
