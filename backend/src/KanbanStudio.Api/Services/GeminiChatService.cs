using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KanbanStudio.Api.Models;

namespace KanbanStudio.Api.Services;

public class GeminiChatService : IAIChatService
{
    public const string DefaultModel = "gemini-3.5-flash-lite";
    public const string FallbackModel = "gemini-3.8-flash";

    private const string SystemInstruction = """
        You are the AI Project Manager assistant for Kanban Studio.
        You help users manage their Kanban project board.

        Rules for response:
        1. Always provide a helpful, friendly, and concise message in 'reply'.
        2. If the user asks you to modify the board (create, edit, move, delete cards, or rename columns):
           - You MUST return the complete updated board in 'board'.
           - Maintain all unchanged columns, cards, and existing IDs.
           - For any new card, create a unique ID (e.g. 'card-' followed by a number, timestamp, or short slug).
           - Ensure every card has a corresponding entry in 'cards' and its ID is placed in the appropriate column's 'cardIds'.
           - When deleting a card, remove it from 'cards' and remove its ID from all column 'cardIds'.
           - When moving a card, remove its ID from the source column's 'cardIds' and append to the destination column's 'cardIds'.
        3. If the user's message is conversational or an informational question (e.g. greetings, questions about existing cards or columns, general advice) where no board changes are requested:
           - You MUST set 'board' to null.
        """;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiChatService> _logger;

    public GeminiChatService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiChatService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        LoadDotEnvIfPresent();
    }

    public static void LoadDotEnvIfPresent()
    {
        var candidates = new List<string>();
        foreach (var startDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(startDir);
            while (current != null)
            {
                candidates.Add(Path.Combine(current.FullName, ".env"));
                current = current.Parent;
            }
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var val = parts[1].Trim().Trim('"', '\'');
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                }
                break;
            }
        }
    }

    public string GetApiKey()
    {
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
               ?? _configuration["GEMINI_API_KEY"] 
               ?? _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("GEMINI_API_KEY is not set in environment or .env file");
        }
        return key;
    }

    public string GetModel()
    {
        return Environment.GetEnvironmentVariable("GEMINI_MODEL") 
            ?? _configuration["GEMINI_MODEL"] 
            ?? _configuration["Gemini:Model"] 
            ?? DefaultModel;
    }

    public async Task<AIChatResponse> ChatAsync(string message, List<ChatMessage> history, BoardData currentBoard, CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        var model = GetModel();

        try
        {
            return await CallGeminiAsync(model, apiKey, message, history, currentBoard, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && model != FallbackModel)
        {
            _logger.LogWarning("Gemini model {Model} returned 503 Service Unavailable, retrying with fallback model {FallbackModel}", model, FallbackModel);
            return await CallGeminiAsync(FallbackModel, apiKey, message, history, currentBoard, cancellationToken);
        }
    }

    private async Task<AIChatResponse> CallGeminiAsync(
        string model,
        string apiKey,
        string message,
        List<ChatMessage> history,
        BoardData currentBoard,
        CancellationToken cancellationToken)
    {
        var boardSummaryObj = new
        {
            columns = currentBoard.Columns,
            cards = currentBoard.Cards.Values.ToList()
        };
        var boardSummaryJson = JsonSerializer.Serialize(boardSummaryObj);

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("Current Kanban Board:");
        promptBuilder.AppendLine("```json");
        promptBuilder.AppendLine(boardSummaryJson);
        promptBuilder.AppendLine("```");
        promptBuilder.AppendLine();

        if (history != null && history.Count > 0)
        {
            promptBuilder.AppendLine("Conversation History:");
            foreach (var msg in history)
            {
                var roleLabel = msg.Role == "user" ? "User" : "Assistant";
                promptBuilder.AppendLine($"{roleLabel}: {msg.Content}");
            }
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine($"User: {message}");
        var finalPrompt = promptBuilder.ToString();

        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new object[]
                {
                    new { text = SystemInstruction }
                }
            },
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = finalPrompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2,
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        reply = new { type = "STRING" },
                        board = new
                        {
                            type = "OBJECT",
                            nullable = true,
                            properties = new
                            {
                                columns = new
                                {
                                    type = "ARRAY",
                                    items = new
                                    {
                                        type = "OBJECT",
                                        properties = new
                                        {
                                            id = new { type = "STRING" },
                                            title = new { type = "STRING" },
                                            cardIds = new
                                            {
                                                type = "ARRAY",
                                                items = new { type = "STRING" }
                                            }
                                        },
                                        required = new[] { "id", "title", "cardIds" }
                                    }
                                },
                                cards = new
                                {
                                    type = "ARRAY",
                                    items = new
                                    {
                                        type = "OBJECT",
                                        properties = new
                                        {
                                            id = new { type = "STRING" },
                                            title = new { type = "STRING" },
                                            details = new { type = "STRING" }
                                        },
                                        required = new[] { "id", "title" }
                                    }
                                }
                            },
                            required = new[] { "columns", "cards" }
                        }
                    },
                    required = new[] { "reply" }
                }
            }
        };

        var requestJson = JsonSerializer.Serialize(requestBody);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API error (Status {(int)response.StatusCode}): {responseContent}", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        var text = root
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return new AIChatResponse { Reply = "No response from AI." };
        }

        var parsed = JsonSerializer.Deserialize<AIGeminiResponse>(text);
        if (parsed == null)
        {
            return new AIChatResponse { Reply = text };
        }

        BoardData? convertedBoard = null;
        if (parsed.Board != null)
        {
            convertedBoard = new BoardData
            {
                Columns = parsed.Board.Columns ?? new List<Column>(),
                Cards = (parsed.Board.Cards ?? new List<Card>())
                    .ToDictionary(c => c.Id, c => c)
            };
        }

        return new AIChatResponse
        {
            Reply = parsed.Reply ?? "",
            Board = convertedBoard
        };
    }

    private class AIGeminiResponse
    {
        [JsonPropertyName("reply")]
        public string? Reply { get; set; }

        [JsonPropertyName("board")]
        public AIGeminiBoard? Board { get; set; }
    }

    private class AIGeminiBoard
    {
        [JsonPropertyName("columns")]
        public List<Column>? Columns { get; set; }

        [JsonPropertyName("cards")]
        public List<Card>? Cards { get; set; }
    }
}
