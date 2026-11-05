using System.Text.Json.Serialization;

namespace KanbanStudio.Api.Models;

public class Card
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;
}

public class Column
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("cardIds")]
    public List<string> CardIds { get; set; } = new();
}

public class BoardData
{
    [JsonPropertyName("columns")]
    public List<Column> Columns { get; set; } = null!;

    [JsonPropertyName("cards")]
    public Dictionary<string, Card> Cards { get; set; } = null!;
}
