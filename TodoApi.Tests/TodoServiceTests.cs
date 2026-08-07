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
    }
}