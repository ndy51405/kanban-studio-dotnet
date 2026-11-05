using KanbanStudio.Api.Models;

namespace KanbanStudio.Api.Services;

public interface IBoardAuditLogger
{
    void LogBoardChanges(BoardData? oldBoard, BoardData newBoard, string source);
}

public class BoardAuditLogger : IBoardAuditLogger
{
    private readonly ILogger<BoardAuditLogger> _logger;

    public BoardAuditLogger(ILogger<BoardAuditLogger> logger)
    {
        _logger = logger;
    }

    public void LogBoardChanges(BoardData? oldBoard, BoardData newBoard, string source)
    {
        var oldColMap = BuildCardColumnMap(oldBoard);
        var newColMap = BuildCardColumnMap(newBoard);

        var oldCards = oldBoard?.Cards ?? new Dictionary<string, Card>();
        var newCards = newBoard.Cards ?? new Dictionary<string, Card>();

        // Detect created cards
        foreach (var (cardId, newCard) in newCards)
        {
            if (!oldCards.ContainsKey(cardId))
            {
                newColMap.TryGetValue(cardId, out var colId);
                _logger.LogInformation(
                    "Card created: {CardId}, Title: {CardTitle}, Column: {ColumnId}, Source: {Source}",
                    cardId,
                    newCard.Title,
                    colId,
                    source
                );
            }
        }

        // Detect deleted cards
        foreach (var (cardId, oldCard) in oldCards)
        {
            if (!newCards.ContainsKey(cardId))
            {
                oldColMap.TryGetValue(cardId, out var colId);
                _logger.LogInformation(
                    "Card deleted: {CardId}, Title: {CardTitle}, Column: {ColumnId}, Source: {Source}",
                    cardId,
                    oldCard.Title,
                    colId,
                    source
                );
            }
        }

        // Detect moved cards and content changes
        foreach (var (cardId, newCard) in newCards)
        {
            if (oldCards.TryGetValue(cardId, out var oldCard))
            {
                oldColMap.TryGetValue(cardId, out var oldCol);
                newColMap.TryGetValue(cardId, out var newCol);

                if (oldCol != null && newCol != null && oldCol != newCol)
                {
                    _logger.LogInformation(
                        "Card moved: {CardId}, Title: {CardTitle}, FromColumn: {FromColumn}, ToColumn: {ToColumn}, Source: {Source}",
                        cardId,
                        newCard.Title,
                        oldCol,
                        newCol,
                        source
                    );
                }

                if (oldCard.Title != newCard.Title || oldCard.Details != newCard.Details)
                {
                    _logger.LogInformation(
                        "Card updated: {CardId}, Title: {CardTitle}, Column: {ColumnId}, Source: {Source}",
                        cardId,
                        newCard.Title,
                        newCol,
                        source
                    );
                }
            }
        }
    }

    private static Dictionary<string, string> BuildCardColumnMap(BoardData? board)
    {
        var map = new Dictionary<string, string>();
        if (board?.Columns == null) return map;

        foreach (var col in board.Columns)
        {
            if (col.CardIds == null) continue;
            foreach (var cardId in col.CardIds)
            {
                map[cardId] = col.Id;
            }
        }

        return map;
    }
}
