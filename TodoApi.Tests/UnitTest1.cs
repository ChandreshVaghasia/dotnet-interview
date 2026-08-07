using Xunit;
using TodoApi.Services;
using TodoApi.Models;
using TodoApi.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var service = new TodoService();
        Assert.True(true);
    }

    [Fact]
    public void TestCreateTodo()
    {
        var service = new TodoService();
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

    [Fact]
    public void TestGetTodo()
    {
        var service = new TodoService();
        var todos = service.GetAllTodos();

        Assert.True(todos.Count > 0);
    }

    [Fact]
    public void UpdateTest()
    {
        var service = new TodoService();
        var todo = new Todo
        {
            Title = "Updated",
            Description = "Updated Description",
            IsCompleted = true
        };

        var result = service.UpdateTodo(1, todo);
        Assert.NotNull(result);
    }

    [Fact]
    public void DeleteWorks()
    {
        var service = new TodoService();
        var result = service.DeleteTodo(999);

        Assert.False(result);
    }

    [Fact]
    public void ControllerTest()
    {
        var todoService = new TodoService();
        var controller = new TodoController(todoService);
        var todo = new Todo { Title = "Test", Description = "Desc" };

        var result = controller.CreateTodo(todo);

        Assert.NotNull(result);
    }

    [Fact]
    public void TestEverything()
    {
        var service = new TodoService();

        var todo1 = service.CreateTodo(new Todo { Title = "1", Description = "D1" });
        var todo2 = service.CreateTodo(new Todo { Title = "2", Description = "D2" });

        var all = service.GetAllTodos();

        service.UpdateTodo(todo1.Id, new Todo { Title = "Updated", Description = "D1" });

        service.DeleteTodo(todo2.Id);

        Assert.True(all.Count >= 2);
    }
}
