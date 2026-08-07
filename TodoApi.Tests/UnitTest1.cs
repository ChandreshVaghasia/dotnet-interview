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

namespace TodoApi.Tests;

public class UnitTest1
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

    [Fact]
    public void Test1()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var service = new TodoService(CreateConfiguration(dbPath));
            Assert.True(true);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
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
            if (File.Exists(dbPath)) File.Delete(dbPath);
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
            if (File.Exists(dbPath)) File.Delete(dbPath);
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
            if (File.Exists(dbPath)) File.Delete(dbPath);
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
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public void ControllerTest()
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
            if (File.Exists(dbPath)) File.Delete(dbPath);
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
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}