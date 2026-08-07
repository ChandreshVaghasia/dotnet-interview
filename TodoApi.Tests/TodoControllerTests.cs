using Xunit;
using TodoApi.Services;
using TodoApi.Models;
using TodoApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace TodoApi.Tests
{
    public class TodoControllerTests
    {
        [Fact]
        public void Controller_Create_ReturnsCreated()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var todoService = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var controller = new TodoController(todoService, NullLogger<TodoController>.Instance);
                var todo = new Todo { Title = "Test", Description = "Desc" };

                var actionResult = controller.CreateTodo(todo);

                Assert.IsType<CreatedAtActionResult>(actionResult.Result);
                var createdResult = actionResult.Result as CreatedAtActionResult;
                Assert.NotNull(createdResult);
                var createdTodo = createdResult.Value as Todo;
                Assert.NotNull(createdTodo);
                Assert.True(createdTodo.Id > 0);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_GetById_ReturnsOk_WhenExists()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
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
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_GetById_ReturnsNotFound_WhenMissing()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var controller = new TodoController(service, NullLogger<TodoController>.Instance);

                var actionResult = controller.GetTodoById(123456);

                Assert.IsType<NotFoundResult>(actionResult.Result);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_Update_ReturnsNotFound_ForMissingId()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
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
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_Update_ReturnsOk_ForExisting()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
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
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_Delete_ReturnsNoContent_WhenDeleted()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
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
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_Delete_ReturnsNotFound_WhenMissing()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var controller = new TodoController(service, NullLogger<TodoController>.Instance);

                var result = controller.DeleteTodo(99999);

                Assert.IsType<NotFoundResult>(result);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_Create_ReturnsBadRequest_ForInvalidModel()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var todoService = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var controller = new TodoController(todoService, NullLogger<TodoController>.Instance);

                // model validation failure (ApiController wouldn't run validation when calling method directly)
                controller.ModelState.AddModelError("Title", "Required");

                var todo = new Todo { Title = "", Description = "Desc" };

                var actionResult = controller.CreateTodo(todo);

                Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }
    }
}