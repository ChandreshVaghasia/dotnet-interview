using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TodoApi.Models;

namespace TodoApi.Services
{
    /// <summary>
    /// Provides CRUD operations for Todo items with optimistic concurrency and in-memory caching.
    /// </summary>
    public class TodoService : ITodoService
    {
        private readonly string _connectionString;
        private readonly IMemoryCache _cache;
        private readonly object _cacheLock = new();
        private const int MaxPageSize = 100;

        // Cache settings (adjust as needed)
        private static readonly TimeSpan PageCacheDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ItemCacheDuration = TimeSpan.FromMinutes(5);
        private const string CacheVersionKey = "todos:version";

        public TodoService(IConfiguration configuration, IMemoryCache? cache = null)
        {
            _connectionString = configuration.GetConnectionString("TodoDatabase")
                ?? throw new InvalidOperationException("Connection string 'TodoDatabase' was not found.");

            // If cache not provided (tests), create a local MemoryCache instance
            _cache = cache ?? new MemoryCache(new MemoryCacheOptions());

            EnsureDatabaseAndTable();
        }

        private void EnsureDatabaseAndTable()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Create table for new DBs (includes Version column)
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

            // For existing DBs, ensure the Version column is present.
            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA table_info('Todos');";
            using var reader = pragmaCmd.ExecuteReader();
            bool hasVersion = false;
            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                if (string.Equals(columnName, "Version", StringComparison.OrdinalIgnoreCase))
                {
                    hasVersion = true;
                    break;
                }
            }
            reader.Close();

            if (!hasVersion)
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Todos ADD COLUMN Version INTEGER NOT NULL DEFAULT 1;";
                alterCmd.ExecuteNonQuery();
            }
        }

        // Get the current cache version token (creates it if missing)
        private long GetVersionToken()
        {
            if (_cache.TryGetValue<long>(CacheVersionKey, out var token))
            {
                return token;
            }

            lock (_cacheLock)
            {
                if (_cache.TryGetValue<long>(CacheVersionKey, out token))
                {
                    return token;
                }

                token = 0L;
                _cache.Set(CacheVersionKey, token);
                return token;
            }
        }

        // Increment and return the new token (used to invalidate old cache keys)
        private long BumpVersionToken()
        {
            lock (_cacheLock)
            {
                if (!_cache.TryGetValue<long>(CacheVersionKey, out var token))
                {
                    token = 0L;
                }

                token++;
                _cache.Set(CacheVersionKey, token);
                return token;
            }
        }

        private string GetItemKey(int id, long token) => $"todo:{id}:v{token}";
        private string GetPageKey(int pageNumber, int pageSize, long token) => $"todos:page:{pageNumber}:{pageSize}:v{token}";

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

            // Invalidate cached pages by bumping the cache version token
            var newToken = BumpVersionToken();

            // Optionally cache the created item under the new token
            var itemKey = GetItemKey(todo.Id, newToken);
            _cache.Set(itemKey, todo, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ItemCacheDuration });

            return todo;
        }

        public List<Todo> GetAllTodos()
        {
            // Non-paged full list - we may choose to not cache this, or reuse paging with large page size.
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

        /// <summary>
        /// Returns Todos in a paginated manner using LIMIT/OFFSET and a separate COUNT query.
        /// Uses IMemoryCache for pages; pages are invalidated implicitly by bumping the cache version token on mutations.
        /// </summary>
        public PaginatedResult<Todo> GetTodosPaged(int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var offset = (pageNumber - 1) * pageSize;
            var items = new List<Todo>();
            int totalCount = 0;

            var token = GetVersionToken();
            var pageKey = GetPageKey(pageNumber, pageSize, token);

            // Try cached page first
            if (_cache.TryGetValue<PaginatedResult<Todo>>(pageKey, out var cached))
            {
                return cached;
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Get total count
            using (var countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = "SELECT COUNT(1) FROM Todos;";
                var countObj = countCmd.ExecuteScalar();
                totalCount = Convert.ToInt32(countObj);
            }

            // Get page items
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT Id, Title, Description, IsCompleted, CreatedAt, Version
                    FROM Todos
                    ORDER BY Id ASC
                    LIMIT @limit OFFSET @offset;
                ";
                cmd.Parameters.AddWithValue("@limit", pageSize);
                cmd.Parameters.AddWithValue("@offset", offset);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var title = reader.GetString(1);
                    var description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    var isCompleted = reader.GetInt32(3) == 1;
                    var createdAtText = reader.IsDBNull(4) ? DateTime.UtcNow.ToString("o") : reader.GetString(4);
                    var createdAt = DateTime.Parse(createdAtText, null, System.Globalization.DateTimeStyles.RoundtripKind);
                    var version = reader.IsDBNull(5) ? 1 : reader.GetInt32(5);

                    items.Add(new Todo
                    {
                        Id = id,
                        Title = title,
                        Description = description,
                        IsCompleted = isCompleted,
                        CreatedAt = createdAt,
                        Version = version
                    });
                }
            }

            var result = new PaginatedResult<Todo>(items, totalCount, pageNumber, pageSize);

            // Cache the page under the current token
            _cache.Set(pageKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = PageCacheDuration });

            return result;
        }

        public Todo? GetTodoById(int id)
        {
            var token = GetVersionToken();
            var itemKey = GetItemKey(id, token);

            if (_cache.TryGetValue<Todo>(itemKey, out var cachedTodo))
            {
                return cachedTodo;
            }

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

                var todo = new Todo
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Description = description,
                    IsCompleted = reader.GetInt32(3) == 1,
                    CreatedAt = createdAt,
                    Version = version
                };

                _cache.Set(itemKey, todo, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ItemCacheDuration });

                return todo;
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

            // Invalidate pages and set updated item cache under new token
            var newToken = BumpVersionToken();
            var itemKey = GetItemKey(id, newToken);
            _cache.Set(itemKey, todo, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ItemCacheDuration });

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

            if (rowsAffected > 0)
            {
                // Invalidate pages by bumping token, item caches for old tokens become stale.
                BumpVersionToken();
                return true;
            }

            return false;
        }
    }
}