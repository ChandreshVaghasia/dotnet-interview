using Xunit;
using TodoApi.Services;
using TodoApi.Models;
using System.IO;

namespace TodoApi.Tests
{
    public class TodoServiceTests
    {
        [Fact]
        public void ServiceConstructionCreatesDatabaseAndTable()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                Assert.NotNull(service);

                var todos = service.GetAllTodos();
                Assert.NotNull(todos);
                Assert.Empty(todos);

                Assert.True(File.Exists(dbPath));
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void TestCreateTodo()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var todo = new Todo
                {
                    Title = "Test",
                    Description = "Test Description",
                    IsCompleted = false
                };

                var result = service.CreateTodo(todo);

                Assert.NotNull(result);
                Assert.True(result.Id > 0);
                Assert.Equal(1, result.Version);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void TestGetTodo()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var created = service.CreateTodo(new Todo { Title = "T1", Description = "D1" });

                var todos = service.GetAllTodos();

                Assert.True(todos.Count > 0);
                Assert.Contains(todos, t => t.Id == created.Id);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void GetByIdNotFoundTest()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var result = service.GetTodoById(123456789);
                Assert.Null(result);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void UpdateTest()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var created = service.CreateTodo(new Todo { Title = "Orig", Description = "Orig Desc", IsCompleted = false });

                var todo = new Todo
                {
                    Title = "Updated",
                    Description = "Updated Description",
                    IsCompleted = true
                };

                var result = service.UpdateTodo(created.Id, todo, created.Version);
                Assert.NotNull(result);

                var reloaded = service.GetTodoById(created.Id);
                Assert.Equal("Updated", reloaded!.Title);
                Assert.True(reloaded.IsCompleted);
                Assert.Equal(created.Version + 1, reloaded.Version);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void CreateWithQuotesTest()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var titleWithQuotes = "O'Reilly \"Special\" Test";
                var created = service.CreateTodo(new Todo { Title = titleWithQuotes, Description = "desc" });

                var fetched = service.GetTodoById(created.Id);
                Assert.NotNull(fetched);
                Assert.Equal(titleWithQuotes, fetched!.Title);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void UpdateNotFoundTest()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var todo = new Todo { Title = "Doesn't matter", Description = "No row", IsCompleted = false };

                var result = service.UpdateTodo(99999, todo, 1);
                Assert.Null(result);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void DeleteWorks()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var result = service.DeleteTodo(999);

                Assert.False(result);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void DeleteAfterCreateTest()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var created = service.CreateTodo(new Todo { Title = "ToDelete", Description = "temp" });

                var deleted = service.DeleteTodo(created.Id);
                Assert.True(deleted);

                var fetched = service.GetTodoById(created.Id);
                Assert.Null(fetched);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void TestEverything()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));

                var todo1 = service.CreateTodo(new Todo { Title = "1", Description = "D1" });
                var todo2 = service.CreateTodo(new Todo { Title = "2", Description = "D2" });

                var all = service.GetAllTodos();

                service.UpdateTodo(todo1.Id, new Todo { Title = "Updated", Description = "D1" }, todo1.Version);

                service.DeleteTodo(todo2.Id);

                Assert.True(all.Count >= 2);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        // New concurrency-related test: service throws ConcurrencyException when version mismatches
        [Fact]
        public void Update_ThrowsConcurrencyException_OnVersionMismatch()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));

                // create an item
                var created = service.CreateTodo(new Todo { Title = "Concurrent", Description = "initial" });

                // another client updates the row (advances the version)
                var otherUpdate = service.UpdateTodo(created.Id, new Todo { Title = "OtherUpdate", Description = "x", IsCompleted = false }, created.Version);
                Assert.NotNull(otherUpdate);
                Assert.Equal(created.Version + 1, otherUpdate.Version);

                // attempt to update with the stale version should throw ConcurrencyException
                var staleUpdate = new Todo { Title = "Stale", Description = "stale attempt", IsCompleted = true };
                Assert.Throws<ConcurrencyException>(() => service.UpdateTodo(created.Id, staleUpdate, created.Version));
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        // --- Pagination service tests ---

        [Fact]
        public void GetTodosPaged_ReturnsExpectedPageAndMetadata()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));

                // Create 25 items
                for (int i = 1; i <= 25; i++)
                {
                    service.CreateTodo(new Todo { Title = $"T{i}", Description = $"D{i}" });
                }

                // Page 2, pageSize 10 => items 11..20
                var pageNumber = 2;
                var pageSize = 10;
                var result = service.GetTodosPaged(pageNumber, pageSize);

                Assert.NotNull(result);
                Assert.Equal(pageNumber, result.PageNumber);
                Assert.Equal(pageSize, result.PageSize);
                Assert.Equal(25, result.TotalCount);
                Assert.Equal(3, result.TotalPages);
                Assert.Equal(pageSize, result.Items.Count);

                var firstOnPage = result.Items.First();
                Assert.Equal("T11", firstOnPage.Title);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void GetTodosPaged_NormalizesBounds_DefaultsApplied()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));

                // Create 5 items only
                for (int i = 1; i <= 5; i++)
                {
                    service.CreateTodo(new Todo { Title = $"Item{i}", Description = $"D{i}" });
                }

                // Pass invalid pageNumber and pageSize (service normalizes)
                var result = service.GetTodosPaged(0, 0);

                // Default pageNumber = 1, default pageSize = 20 (so all 5 returned)
                Assert.Equal(1, result.PageNumber);
                Assert.Equal(20, result.PageSize);
                Assert.Equal(5, result.TotalCount);
                Assert.Equal(1, result.TotalPages);
                Assert.Equal(5, result.Items.Count);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void GetTodosPaged_CachesResults_And_InvalidatesOnCreate()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));

                // Create initial items
                for (int i = 1; i <= 3; i++)
                    service.CreateTodo(new Todo { Title = $"T{i}", Description = $"D{i}" });

                // First call caches the page
                var page1 = service.GetTodosPaged(1, 10);

                // Second call should return the same cached object instance
                var page2 = service.GetTodosPaged(1, 10);
                Assert.Same(page1, page2);

                var prevTotal = page1.TotalCount;

                // Create a new item -> should bump the cache token and invalidate pages
                service.CreateTodo(new Todo { Title = "New", Description = "added" });

                var pageAfterCreate = service.GetTodosPaged(1, 10);

                // New page object expected (old cached page should be stale)
                Assert.NotSame(page1, pageAfterCreate);
                Assert.Equal(prevTotal + 1, pageAfterCreate.TotalCount);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void GetTodoById_CachesResult_And_InvalidatesOnUpdate()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));

                // Create item
                var created = service.CreateTodo(new Todo { Title = "Original", Description = "orig" });

                // First read caches the item
                var t1 = service.GetTodoById(created.Id);
                Assert.NotNull(t1);

                // Second read should be served from cache (same instance)
                var t2 = service.GetTodoById(created.Id);
                Assert.Same(t1, t2);

                // Update the item using current version
                var updatedTodo = new Todo { Title = "Updated", Description = "updated", IsCompleted = true };
                var updated = service.UpdateTodo(created.Id, updatedTodo, created.Version);
                Assert.NotNull(updated);
                Assert.Equal(created.Version + 1, updated!.Version);

                // After update, cache should be invalidated (new token), GetTodoById should return updated data (different instance)
                var t3 = service.GetTodoById(created.Id);
                Assert.NotNull(t3);
                Assert.NotSame(t1, t3); // cached old instance was invalidated
                Assert.Equal("Updated", t3!.Title);
                Assert.Equal(created.Version + 1, t3.Version);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void GetTodoById_InvalidatedOnDelete_ReturnsNullAfterDelete()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));

                // Create and cache item
                var created = service.CreateTodo(new Todo { Title = "ToDelete", Description = "tmp" });
                var t1 = service.GetTodoById(created.Id);
                Assert.NotNull(t1);

                // Delete the item (this bumps the cache token)
                var deleted = service.DeleteTodo(created.Id);
                Assert.True(deleted);

                // After delete, GetTodoById should return null
                var after = service.GetTodoById(created.Id);
                Assert.Null(after);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }
    }
}