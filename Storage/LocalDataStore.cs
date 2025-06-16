using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace AiShell.Storage;

public class LocalDataStore
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public LocalDataStore()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AiShell");
        Directory.CreateDirectory(appDataPath);
        
        _dbPath = Path.Combine(appDataPath, "aishell.db");
        _connectionString = $"Data Source={_dbPath}";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var createTables = @"
            CREATE TABLE IF NOT EXISTS Variables (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT 'user',
                CreatedAt TEXT NOT NULL,
                LastUsed TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Aliases (
                Name TEXT PRIMARY KEY,
                Command TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UsageCount INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS CommandHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                NaturalInput TEXT NOT NULL,
                GeneratedCommand TEXT NOT NULL,
                Success INTEGER NOT NULL,
                ExecutionTime TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );
        ";

        using var command = new SqliteCommand(createTables, connection);
        command.ExecuteNonQuery();
    }

    public void SetVariable(string key, string value, string category = "user")
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            INSERT OR REPLACE INTO Variables (Key, Value, Category, CreatedAt, LastUsed)
            VALUES (@key, @value, @category, @created, @used)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@category", category);
        command.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@used", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        command.ExecuteNonQuery();
    }

    public string? GetVariable(string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            UPDATE Variables SET LastUsed = @used WHERE Key = @key;
            SELECT Value FROM Variables WHERE Key = @key;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@used", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return command.ExecuteScalar()?.ToString();
    }

    public Dictionary<string, VariableEntry> GetAllVariables()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = "SELECT Key, Value, Category, CreatedAt, LastUsed FROM Variables";
        using var command = new SqliteCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var variables = new Dictionary<string, VariableEntry>();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            variables[key] = new VariableEntry
            {
                Value = reader.GetString(1),
                Category = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                LastUsed = DateTime.Parse(reader.GetString(4))
            };
        }

        return variables;
    }

    public void SetAlias(string name, string command)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            INSERT OR REPLACE INTO Aliases (Name, Command, CreatedAt, UsageCount)
            VALUES (@name, @command, @created, COALESCE((SELECT UsageCount FROM Aliases WHERE Name = @name), 0))
        ";

        using var command_db = new SqliteCommand(sql, connection);
        command_db.Parameters.AddWithValue("@name", name);
        command_db.Parameters.AddWithValue("@command", command);
        command_db.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        command_db.ExecuteNonQuery();
    }

    public string? GetAlias(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            UPDATE Aliases SET UsageCount = UsageCount + 1 WHERE Name = @name;
            SELECT Command FROM Aliases WHERE Name = @name;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name);

        return command.ExecuteScalar()?.ToString();
    }

    public void SaveCommandHistory(string naturalInput, string generatedCommand, bool success, TimeSpan executionTime)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            INSERT INTO CommandHistory (NaturalInput, GeneratedCommand, Success, ExecutionTime, Timestamp)
            VALUES (@input, @command, @success, @time, @timestamp)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@input", naturalInput);
        command.Parameters.AddWithValue("@command", generatedCommand);
        command.Parameters.AddWithValue("@success", success ? 1 : 0);
        command.Parameters.AddWithValue("@time", executionTime.ToString());
        command.Parameters.AddWithValue("@timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        command.ExecuteNonQuery();
    }
}

public class VariableEntry
{
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = "user";
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsed { get; set; }
}