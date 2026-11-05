using System.Text.Json;
using KanbanStudio.Api.Models;
using KanbanStudio.Api.Services;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Xunit;

namespace KanbanStudio.Tests;

public class BoardAuditLoggerTests
{
    private class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception)));
        }
    }

    private class StringSink : ILogEventSink
    {
        private readonly TextWriter _writer;
        private readonly ITextFormatter _formatter;

        public StringSink(TextWriter writer, ITextFormatter formatter)
        {
            _writer = writer;
            _formatter = formatter;
        }

        public void Emit(LogEvent logEvent)
        {
            _formatter.Format(logEvent, _writer);
        }
    }

    [Fact]
    public void LogBoardChanges_LogsCardCreated()
    {
        var testLogger = new TestLogger<BoardAuditLogger>();
        var auditLogger = new BoardAuditLogger(testLogger);

        var oldBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() { "card-1" } }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new() { Id = "card-1", Title = "Existing Card", Details = "Details" }
            }
        };

        var newBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() { "card-1", "card-2" } }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new() { Id = "card-1", Title = "Existing Card", Details = "Details" },
                ["card-2"] = new() { Id = "card-2", Title = "Brand New Card", Details = "New Details" }
            }
        };

        auditLogger.LogBoardChanges(oldBoard, newBoard, "Test");

        var createdLog = testLogger.Logs.FirstOrDefault(l => l.Message.Contains("Card created"));
        Assert.NotEqual(default, createdLog);
        Assert.Contains("card-2", createdLog.Message);
        Assert.Contains("Brand New Card", createdLog.Message);
        Assert.Contains("col-backlog", createdLog.Message);
    }

    [Fact]
    public void LogBoardChanges_LogsCardDeleted()
    {
        var testLogger = new TestLogger<BoardAuditLogger>();
        var auditLogger = new BoardAuditLogger(testLogger);

        var oldBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() { "card-1" } }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new() { Id = "card-1", Title = "Card to delete", Details = "Details" }
            }
        };

        var newBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() }
            },
            Cards = new Dictionary<string, Card>()
        };

        auditLogger.LogBoardChanges(oldBoard, newBoard, "Test");

        var deletedLog = testLogger.Logs.FirstOrDefault(l => l.Message.Contains("Card deleted"));
        Assert.NotEqual(default, deletedLog);
        Assert.Contains("card-1", deletedLog.Message);
        Assert.Contains("Card to delete", deletedLog.Message);
    }

    [Fact]
    public void LogBoardChanges_LogsCardMoved()
    {
        var testLogger = new TestLogger<BoardAuditLogger>();
        var auditLogger = new BoardAuditLogger(testLogger);

        var oldBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() { "card-1" } },
                new() { Id = "col-done", Title = "Done", CardIds = new() }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new() { Id = "card-1", Title = "Moving Card", Details = "Details" }
            }
        };

        var newBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() },
                new() { Id = "col-done", Title = "Done", CardIds = new() { "card-1" } }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new() { Id = "card-1", Title = "Moving Card", Details = "Details" }
            }
        };

        auditLogger.LogBoardChanges(oldBoard, newBoard, "Test");

        var movedLog = testLogger.Logs.FirstOrDefault(l => l.Message.Contains("Card moved"));
        Assert.NotEqual(default, movedLog);
        Assert.Contains("card-1", movedLog.Message);
        Assert.Contains("col-backlog", movedLog.Message);
        Assert.Contains("col-done", movedLog.Message);
    }

    [Fact]
    public void LogBoardChanges_LogsCardUpdated()
    {
        var testLogger = new TestLogger<BoardAuditLogger>();
        var auditLogger = new BoardAuditLogger(testLogger);

        var oldBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() { "card-1" } }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new() { Id = "card-1", Title = "Old Title", Details = "Old Details" }
            }
        };

        var newBoard = new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() { "card-1" } }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new() { Id = "card-1", Title = "New Title", Details = "New Details" }
            }
        };

        auditLogger.LogBoardChanges(oldBoard, newBoard, "Test");

        var updatedLog = testLogger.Logs.FirstOrDefault(l => l.Message.Contains("Card updated"));
        Assert.NotEqual(default, updatedLog);
        Assert.Contains("card-1", updatedLog.Message);
        Assert.Contains("New Title", updatedLog.Message);
    }

    [Fact]
    public void Serilog_OutputsValidJsonFormat()
    {
        using var stringWriter = new StringWriter();
        var formatter = new RenderedCompactJsonFormatter();
        var logger = new LoggerConfiguration()
            .WriteTo.Sink(new StringSink(stringWriter, formatter))
            .CreateLogger();

        logger.Information("Card created: {CardId}, Title: {CardTitle}", "card-test", "Test Card");

        var output = stringWriter.ToString().Trim();
        using var doc = JsonDocument.Parse(output);
        Assert.True(doc.RootElement.TryGetProperty("@m", out var messageProp));
        Assert.Contains("card-test", messageProp.GetString());
        Assert.Contains("Test Card", messageProp.GetString());
        Assert.True(doc.RootElement.TryGetProperty("CardId", out var cardProp));
        Assert.Equal("card-test", cardProp.GetString());
        Assert.True(doc.RootElement.TryGetProperty("CardTitle", out var titleProp));
        Assert.Equal("Test Card", titleProp.GetString());
    }
}
