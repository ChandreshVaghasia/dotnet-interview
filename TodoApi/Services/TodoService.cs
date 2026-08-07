using Microsoft.Data.Sqlite;
using TodoApi.Models;

namespace TodoApi.Services
{
    /// <summary>
    /// Provides CRUD operations for Todo items with optimistic concurrency.
    /// </summary>
    public class TodoService : ITodoService
    {
        private readonly string _connectionString;

        public TodoService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TodoDatabase")
                ?? throw new InvalidOperationException("Connection string 'TodoDatabase' was not found.");

            EnsureDatabaseAndTable();
        }

        private void EnsureDatabaseAndTable()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Todos (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    IsCompleted INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    Version INTEGER NOT NULL DEFAULT 1
                );
            ";
            command.ExecuteNonQuery();
        }

        public Todo CreateTodo(Todo todo)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Todos (Title, Description, IsCompleted, CreatedAt)
                VALUES (@title, @description, @isCompleted, @createdAt);
                SELECT last_insert_rowid();
            ";

            var createdAt = DateTime.UtcNow.ToString("o");

            command.Parameters.AddWithValue("@title", todo.Title ?? string.Empty);
            command.Parameters.AddWithValue("@description", todo.Description ?? string.Empty);
            command.Parameters.AddWithValue("@isCompleted", todo.IsCompleted ? 1 : 0);
            command.Parameters.AddWithValue("@createdAt", createdAt);

            var idObj = command.ExecuteScalar();
            var id = Convert.ToInt32(idObj);

            todo.Id = id;
            todo.CreatedAt = DateTime.Parse(createdAt, null, System.Globalization.DateTimeStyles.RoundtripKind);

            // DB default Version = 1
            todo.Version = 1;

            return todo;
        }

        public List<Todo> GetAllTodos()
        {
            var todos = new List<Todo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Description, IsCompleted, CreatedAt, Version FROM Todos";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var title = reader.GetString(1);
                var description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var isCompleted = reader.GetInt32(3) == 1;
                var createdAtText = reader.IsDBNull(4) ? DateTime.UtcNow.ToString("o") : reader.GetString(4);
                var createdAt = DateTime.Parse(createdAtText, null, System.Globalization.DateTimeStyles.RoundtripKind);
                var version = reader.IsDBNull(5) ? 1 : reader.GetInt32(5);

                todos.Add(new Todo
                {
                    Id = id,
                    Title = title,
                    Description = description,
                    IsCompleted = isCompleted,
                    CreatedAt = createdAt,
                    Version = version
                });
            }

            return todos;
        }

        public Todo? GetTodoById(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Description, IsCompleted, CreatedAt, Version FROM Todos WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var createdAtText = reader.IsDBNull(4) ? DateTime.UtcNow.ToString("o") : reader.GetString(4);
                var createdAt = DateTime.Parse(createdAtText, null, System.Globalization.DateTimeStyles.RoundtripKind);
                var version = reader.IsDBNull(5) ? 1 : reader.GetInt32(5);

                return new Todo
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = description,
                    IsCompleted = reader.GetInt32(3) == 1,
                    CreatedAt = createdAt,
                    Version = version
                };
            }

            return null;
        }

        public Todo? UpdateTodo(int id, Todo todo, int expectedVersion)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Todos
                SET Title = @title, Description = @description, IsCompleted = @isCompleted, Version = Version + 1
                WHERE Id = @id AND Version = @expectedVersion;
            ";
            command.Parameters.AddWithValue("@title", todo.Title ?? string.Empty);
            command.Parameters.AddWithValue("@description", todo.Description ?? string.Empty);
            command.Parameters.AddWithValue("@isCompleted", todo.IsCompleted ? 1 : 0);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@expectedVersion", expectedVersion);

            var rowsAffected = command.ExecuteNonQuery();

            if (rowsAffected == 0)
            {
                // Distinguish between missing row and version conflict
                using var existsCmd = connection.CreateCommand();
                existsCmd.CommandText = "SELECT COUNT(1) FROM Todos WHERE Id = @id";
                existsCmd.Parameters.AddWithValue("@id", id);
                var existsObj = existsCmd.ExecuteScalar();
                var exists = Convert.ToInt32(existsObj) > 0;

                if (!exists)
                {
                    return null; // not found
                }

                // Row exists but version mismatch - concurrency conflict
                throw new ConcurrencyException("The todo item was modified by another process.");
            }

            // Update successful, incremented version on DB: set returned object's Version to expectedVersion + 1
            todo.Id = id;
            todo.Version = expectedVersion + 1;
            return todo;
        }

        public bool DeleteTodo(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Todos WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            var rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
    }
}