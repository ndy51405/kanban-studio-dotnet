using System.Net;
using System.Text;
using System.Text.Json;
using KanbanStudio.Api.Data;
using KanbanStudio.Api.Models;
using KanbanStudio.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KanbanStudio.Tests;

public class AiServiceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public void GetModel_ReturnsDefaultModel_WhenNotConfigured()
    {
        var original = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_MODEL", null);
            var config = new ConfigurationBuilder().Build();
            var service = new GeminiChatService(new HttpClient(), config, NullLogger<GeminiChatService>.Instance);

            Assert.Equal(GeminiChatService.DefaultModel, service.GetModel());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_MODEL", original);
        }
    }

    [Fact]
    public void GetModel_ReturnsCustomModel_WhenConfigured()
    {
        var original = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_MODEL", "custom-model");
            var config = new ConfigurationBuilder().Build();
            var service = new GeminiChatService(new HttpClient(), config, NullLogger<GeminiChatService>.Instance);

            Assert.Equal("custom-model", service.GetModel());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_MODEL", original);
        }
    }

    [Fact]
    public void GetApiKey_ThrowsWhenMissing()
    {
        var original = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            var config = new ConfigurationBuilder().Build();
            var service = new GeminiChatService(new HttpClient(), config, NullLogger<GeminiChatService>.Instance);
            // Clear again in case LoadDotEnvIfPresent loaded a key
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);

            var ex = Assert.Throws<InvalidOperationException>(() => service.GetApiKey());
            Assert.Contains("GEMINI_API_KEY is not set", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", original);
        }
    }

    [Fact]
    public async Task ChatAsync_ParsesStructuredResponse_FromMockHttp()
    {
        HttpRequestMessage? capturedRequest = null;
        var geminiResponsePayload = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = JsonSerializer.Serialize(new
                                {
                                    reply = "Mocked AI Response",
                                    board = (object?)null
                                })
                            }
                        }
                    }
                }
            }
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponsePayload), Encoding.UTF8, "application/json")
            };
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GEMINI_API_KEY"] = "test-api-key",
                ["GEMINI_MODEL"] = "test-model"
            })
            .Build();

        var originalKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var originalModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            Environment.SetEnvironmentVariable("GEMINI_MODEL", null);

            var service = new GeminiChatService(new HttpClient(handler), config, NullLogger<GeminiChatService>.Instance);
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            Environment.SetEnvironmentVariable("GEMINI_MODEL", null);

            var board = DatabaseService.GetInitialBoardData();
            var result = await service.ChatAsync("Hello Gemini", new List<ChatMessage>(), board);

            Assert.Equal("Mocked AI Response", result.Reply);
            Assert.Null(result.Board);
            Assert.NotNull(capturedRequest);
            Assert.Contains("models/test-model:generateContent", capturedRequest.RequestUri?.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalKey);
            Environment.SetEnvironmentVariable("GEMINI_MODEL", originalModel);
        }
    }
}
