using Microsoft.Data.Sqlite;
using TodoApi.Models;

namespace TodoApi.Services
{
    /// <summary>
    /// Provides CRUD operations for Todo items.
    /// </summary>
    public class TodoService : ITodoService
    {
        private string _connectionString = "Data Source=todos.db";

        public TodoService()
        {
        }

        public Todo CreateTodo(Todo todo)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = $@"
                INSERT INTO Todos (Title, Description, IsCompleted, CreatedAt)
                VALUES ('{todo.Title}', '{todo.Description}', {(todo.IsCompleted ? 1 : 0)}, '{DateTime.UtcNow.ToString("o")}');
                SELECT last_insert_rowid();
            ";

            var id = Convert.ToInt32(command.ExecuteScalar());
            todo.Id = id;
            todo.CreatedAt = DateTime.UtcNow;
            return todo;
        }

        public List<Todo> GetAllTodos()
        {
            var todos = new List<Todo>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Todos";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                todos.Add(new Todo
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.GetString(2),
                    IsCompleted = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4))
                });
            }

            return todos;
        }

        public Todo GetTodoById(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM Todos WHERE Id = {id}";

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Todo
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = reader.GetString(2),
                    IsCompleted = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4))
                };
            }

            return null;
        }

        public Todo UpdateTodo(int id, Todo todo)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = $@"
                UPDATE Todos
                SET Title = '{todo.Title}', Description = '{todo.Description}', IsCompleted = {(todo.IsCompleted ? 1 : 0)}
                WHERE Id = {id}
            ";

            var rowsAffected = command.ExecuteNonQuery();

            todo.Id = id;
            return todo;
        }

        public bool DeleteTodo(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM Todos WHERE Id = {id}";

            var rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
    }
}
