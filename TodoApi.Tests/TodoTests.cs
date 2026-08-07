using Xunit;
using TodoApi.Services;
using TodoApi.Models;
using TodoApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;

namespace TodoApi.Tests;

public class TodoTests
{
    private static IConfiguration CreateConfiguration(string dbFilePath)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:TodoDatabase", $"Data Source={dbFilePath}" }
            })
            .Build();
    }

    private static string CreateTempDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"todos_{Guid.NewGuid():N}.db");
    }

    private static void DeleteFileWithRetries(string path, int retries = 5, int delayMs = 200)
    {
        if (!File.Exists(path)) return;

        for (int attempt = 0; attempt < retries; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                // Force finalizers and wait a bit for OS to release file handles, then retry
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(delayMs);
            }
        }

        // Final attempt (suppress any exception to avoid failing cleanup)
        try { File.Delete(path); } catch { }
    }

    [Fact]
    public void ServiceConstructionCreatesDatabaseAndTable()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            Assert.NotNull(service);

            // On construction the service ensures the DB and table exist.
            // GetAllTodos should return an empty list for a fresh DB.
            var todos = service.GetAllTodos();
            Assert.NotNull(todos);
            Assert.Empty(todos);

            // The sqlite file should exist on disk.
            Assert.True(File.Exists(dbPath));
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void TestCreateTodo()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var todo = new Todo
            {
                Title = "Test",
                Description = "Test Description",
                IsCompleted = false
            };

            var result = service.CreateTodo(todo);

            Assert.NotNull(result);
            Assert.True(result.Id > 0);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void TestGetTodo()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            // Arrange: create a todo so GetAllTodos returns at least one
            var created = service.CreateTodo(new Todo { Title = "T1", Description = "D1" });

            var todos = service.GetAllTodos();

            Assert.True(todos.Count > 0);
            Assert.Contains(todos, t => t.Id == created.Id);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void GetByIdNotFoundTest()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var result = service.GetTodoById(123456789);
            Assert.Null(result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void UpdateTest()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            // Arrange: create a todo to update
            var created = service.CreateTodo(new Todo { Title = "Orig", Description = "Orig Desc", IsCompleted = false });

            var todo = new Todo
            {
                Title = "Updated",
                Description = "Updated Description",
                IsCompleted = true
            };

            var result = service.UpdateTodo(created.Id, todo);
            Assert.NotNull(result);

            var reloaded = service.GetTodoById(created.Id);
            Assert.Equal("Updated", reloaded.Title);
            Assert.True(reloaded.IsCompleted);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void CreateWithQuotesTest()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var titleWithQuotes = "O'Reilly \"Special\" Test";
            var created = service.CreateTodo(new Todo { Title = titleWithQuotes, Description = "desc" });

            var fetched = service.GetTodoById(created.Id);
            Assert.NotNull(fetched);
            Assert.Equal(titleWithQuotes, fetched.Title);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void UpdateNotFoundTest()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var todo = new Todo { Title = "Doesn't matter", Description = "No row", IsCompleted = false };

            var result = service.UpdateTodo(99999, todo);
            Assert.Null(result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void DeleteWorks()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var result = service.DeleteTodo(999);

            Assert.False(result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void DeleteAfterCreateTest()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var created = service.CreateTodo(new Todo { Title = "ToDelete", Description = "temp" });

            var deleted = service.DeleteTodo(created.Id);
            Assert.True(deleted);

            var fetched = service.GetTodoById(created.Id);
            Assert.Null(fetched);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_Create_ReturnsCreated()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var todoService = new TodoService(CreateConfiguration(dbPath));
            var controller = new TodoController(todoService, NullLogger<TodoController>.Instance);
            var todo = new Todo { Title = "Test", Description = "Desc" };

            var actionResult = controller.CreateTodo(todo);

            // Expect CreatedAtActionResult
            Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdResult = actionResult.Result as CreatedAtActionResult;
            Assert.NotNull(createdResult);
            var createdTodo = createdResult.Value as Todo;
            Assert.NotNull(createdTodo);
            Assert.True(createdTodo.Id > 0);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_GetById_ReturnsOk_WhenExists()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var created = service.CreateTodo(new Todo { Title = "X", Description = "Y" });

            var controller = new TodoController(service, NullLogger<TodoController>.Instance);

            var actionResult = controller.GetTodoById(created.Id);

            Assert.IsType<OkObjectResult>(actionResult.Result);
            var ok = actionResult.Result as OkObjectResult;
            Assert.NotNull(ok);
            var todo = ok.Value as Todo;
            Assert.NotNull(todo);
            Assert.Equal(created.Id, todo.Id);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_GetById_ReturnsNotFound_WhenMissing()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var controller = new TodoController(service, NullLogger<TodoController>.Instance);

            var actionResult = controller.GetTodoById(123456);

            Assert.IsType<NotFoundResult>(actionResult.Result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_Update_ReturnsNotFound_ForMissingId()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var controller = new TodoController(service, NullLogger<TodoController>.Instance);

            var request = new TodoApi.Models.Requests.UpdateTodoRequest
            {
                Title = "Nope",
                Description = "No row",
                IsCompleted = false
            };

            var actionResult = controller.UpdateTodo(99999, request);

            Assert.IsType<NotFoundResult>(actionResult.Result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_Update_ReturnsOk_ForExisting()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var created = service.CreateTodo(new Todo { Title = "A", Description = "B" });

            var controller = new TodoController(service, NullLogger<TodoController>.Instance);

            var request = new TodoApi.Models.Requests.UpdateTodoRequest
            {
                Title = "Updated",
                Description = "Updated Desc",
                IsCompleted = true
            };

            var actionResult = controller.UpdateTodo(created.Id, request);

            Assert.IsType<OkObjectResult>(actionResult.Result);
            var ok = actionResult.Result as OkObjectResult;
            Assert.NotNull(ok);
            var updated = ok.Value as Todo;
            Assert.NotNull(updated);
            Assert.Equal(created.Id, updated.Id);
            Assert.Equal("Updated", updated.Title);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_Delete_ReturnsNoContent_WhenDeleted()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var created = service.CreateTodo(new Todo { Title = "ToDelete", Description = "d" });

            var controller = new TodoController(service, NullLogger<TodoController>.Instance);

            var result = controller.DeleteTodo(created.Id);

            Assert.IsType<NoContentResult>(result);
            // confirm it's gone
            var get = controller.GetTodoById(created.Id);
            Assert.IsType<NotFoundResult>(get.Result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_Delete_ReturnsNotFound_WhenMissing()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            var controller = new TodoController(service, NullLogger<TodoController>.Instance);

            var result = controller.DeleteTodo(99999);

            Assert.IsType<NotFoundResult>(result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void Controller_Create_ReturnsBadRequest_ForInvalidModel()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var todoService = new TodoService(CreateConfiguration(dbPath));
            var controller = new TodoController(todoService, NullLogger<TodoController>.Instance);

            // model validation failure (ApiController wouldn't run validation when calling method directly)
            controller.ModelState.AddModelError("Title", "Required");

            var todo = new Todo { Title = "", Description = "Desc" };

            var actionResult = controller.CreateTodo(todo);

            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }

    [Fact]
    public void TestEverything()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));

            var todo1 = service.CreateTodo(new Todo { Title = "1", Description = "D1" });
            var todo2 = service.CreateTodo(new Todo { Title = "2", Description = "D2" });

            var all = service.GetAllTodos();

            service.UpdateTodo(todo1.Id, new Todo { Title = "Updated", Description = "D1" });

            service.DeleteTodo(todo2.Id);

            Assert.True(all.Count >= 2);
        }
        finally
        {
            DeleteFileWithRetries(dbPath);
        }
    }
}