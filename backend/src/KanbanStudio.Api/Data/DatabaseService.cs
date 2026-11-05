using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KanbanStudio.Api.Models;
using Microsoft.Data.Sqlite;

namespace KanbanStudio.Api.Data;

public class DatabaseService
{
    private readonly string _dbPath;

    public DatabaseService(string? dbPath = null)
    {
        _dbPath = dbPath ?? ResolveDbPath();
    }

    public string DbPath => _dbPath;

    public static string ResolveDbPath()
    {
        var envPath = Environment.GetEnvironmentVariable("DATABASE_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        if (Directory.Exists("/app/data"))
        {
            return "/app/data/pm.db";
        }

        // Look for data folder relative to working directory or app base
        foreach (var startDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(startDir);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "data");
                if (Directory.Exists(candidate))
                {
                    return Path.Combine(candidate, "pm.db");
                }
                current = current.Parent;
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "data", "pm.db");
    }

    public SqliteConnection GetConnection()
    {
        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connStr);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        cmd.ExecuteNonQuery();

        return connection;
    }

    public void InitDb()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var conn = GetConnection();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT UNIQUE NOT NULL,
                    password_hash TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS boards (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
                    board_json TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
                CREATE INDEX IF NOT EXISTS idx_boards_user_id ON boards(user_id);
            ";
            cmd.ExecuteNonQuery();
        }

        long userId;
        using (var checkUserCmd = conn.CreateCommand())
        {
            checkUserCmd.Transaction = tx;
            checkUserCmd.CommandText = "SELECT id FROM users WHERE username = @username";
            checkUserCmd.Parameters.AddWithValue("@username", "user");
            var result = checkUserCmd.ExecuteScalar();

            var now = DateTime.UtcNow.ToString("o");
            if (result == null)
            {
                using var insertUserCmd = conn.CreateCommand();
                insertUserCmd.Transaction = tx;
                insertUserCmd.CommandText = @"
                    INSERT INTO users (username, password_hash, created_at)
                    VALUES (@username, @password_hash, @created_at);
                    SELECT last_insert_rowid();
                ";
                insertUserCmd.Parameters.AddWithValue("@username", "user");
                insertUserCmd.Parameters.AddWithValue("@password_hash", HashPassword("password"));
                insertUserCmd.Parameters.AddWithValue("@created_at", now);
                userId = (long)insertUserCmd.ExecuteScalar()!;
            }
            else
            {
                userId = Convert.ToInt64(result);
            }
        }

        using (var checkBoardCmd = conn.CreateCommand())
        {
            checkBoardCmd.Transaction = tx;
            checkBoardCmd.CommandText = "SELECT id FROM boards WHERE user_id = @user_id";
            checkBoardCmd.Parameters.AddWithValue("@user_id", userId);
            var result = checkBoardCmd.ExecuteScalar();

            if (result == null)
            {
                var now = DateTime.UtcNow.ToString("o");
                var initialJson = JsonSerializer.Serialize(GetInitialBoardData());

                using var insertBoardCmd = conn.CreateCommand();
                insertBoardCmd.Transaction = tx;
                insertBoardCmd.CommandText = @"
                    INSERT INTO boards (user_id, board_json, updated_at)
                    VALUES (@user_id, @board_json, @updated_at);
                ";
                insertBoardCmd.Parameters.AddWithValue("@user_id", userId);
                insertBoardCmd.Parameters.AddWithValue("@board_json", initialJson);
                insertBoardCmd.Parameters.AddWithValue("@updated_at", now);
                insertBoardCmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    public static string HashPassword(string password, string? salt = null)
    {
        salt ??= Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{salt}:{password}");
        var hash = Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
        return $"{salt}:{hash}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split(':', 2);
            if (parts.Length != 2) return false;
            var salt = parts[0];
            return HashPassword(password, salt) == storedHash;
        }
        catch
        {
            return false;
        }
    }

    public bool AuthenticateUser(string username, string password)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT password_hash FROM users WHERE username = @username";
        cmd.Parameters.AddWithValue("@username", username);

        var result = cmd.ExecuteScalar() as string;
        if (result == null) return false;

        return VerifyPassword(password, result);
    }

    public BoardData? GetBoardData(string username = "user")
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT b.board_json
            FROM boards b
            JOIN users u ON b.user_id = u.id
            WHERE u.username = @username
        ";
        cmd.Parameters.AddWithValue("@username", username);

        var json = cmd.ExecuteScalar() as string;
        if (string.IsNullOrEmpty(json)) return null;

        return JsonSerializer.Deserialize<BoardData>(json);
    }

    public bool UpdateBoardData(string username, BoardData boardData)
    {
        using var conn = GetConnection();
        using var userCmd = conn.CreateCommand();
        userCmd.CommandText = "SELECT id FROM users WHERE username = @username";
        userCmd.Parameters.AddWithValue("@username", username);

        var userIdObj = userCmd.ExecuteScalar();
        if (userIdObj == null) return false;

        var userId = Convert.ToInt64(userIdObj);
        var now = DateTime.UtcNow.ToString("o");
        var json = JsonSerializer.Serialize(boardData);

        using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = @"
            UPDATE boards
            SET board_json = @board_json, updated_at = @updated_at
            WHERE user_id = @user_id
        ";
        updateCmd.Parameters.AddWithValue("@board_json", json);
        updateCmd.Parameters.AddWithValue("@updated_at", now);
        updateCmd.Parameters.AddWithValue("@user_id", userId);

        var rows = updateCmd.ExecuteNonQuery();
        return rows > 0;
    }

    public static BoardData GetInitialBoardData()
    {
        return new BoardData
        {
            Columns = new List<Column>
            {
                new() { Id = "col-backlog", Title = "Backlog", CardIds = new() { "card-1", "card-2" } },
                new() { Id = "col-discovery", Title = "Discovery", CardIds = new() { "card-3" } },
                new() { Id = "col-progress", Title = "In Progress", CardIds = new() { "card-4", "card-5" } },
                new() { Id = "col-review", Title = "Review", CardIds = new() { "card-6" } },
                new() { Id = "col-done", Title = "Done", CardIds = new() { "card-7", "card-8" } }
            },
            Cards = new Dictionary<string, Card>
            {
                ["card-1"] = new()
                {
                    Id = "card-1",
                    Title = "Align roadmap themes",
                    Details = "Draft quarterly themes with impact statements and metrics."
                },
                ["card-2"] = new()
                {
                    Id = "card-2",
                    Title = "Gather customer signals",
                    Details = "Review support tags, sales notes, and churn feedback."
                },
                ["card-3"] = new()
                {
                    Id = "card-3",
                    Title = "Prototype analytics view",
                    Details = "Sketch initial dashboard layout and key drill-downs."
                },
                ["card-4"] = new()
                {
                    Id = "card-4",
                    Title = "Refine status language",
                    Details = "Standardize column labels and tone across the board."
                },
                ["card-5"] = new()
                {
                    Id = "card-5",
                    Title = "Design card layout",
                    Details = "Add hierarchy and spacing for scanning dense lists."
                },
                ["card-6"] = new()
                {
                    Id = "card-6",
                    Title = "QA micro-interactions",
                    Details = "Verify hover, focus, and loading states."
                },
                ["card-7"] = new()
                {
                    Id = "card-7",
                    Title = "Ship marketing page",
                    Details = "Final copy approved and asset pack delivered."
                },
                ["card-8"] = new()
                {
                    Id = "card-8",
                    Title = "Close onboarding sprint",
                    Details = "Document release notes and share internally."
                }
            }
        };
    }
}
