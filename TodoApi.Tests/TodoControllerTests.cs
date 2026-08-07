using Xunit;
using TodoApi.Services;
using TodoApi.Models;
using TodoApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http; // for StatusCodes

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
                var request = new TodoApi.Models.Requests.CreateTodoRequest { Title = "Test", Description = "Desc", IsCompleted = false };

                var actionResult = controller.CreateTodo(request);

                Assert.IsType<CreatedAtActionResult>(actionResult.Result);
                var createdResult = actionResult.Result as CreatedAtActionResult;
                Assert.NotNull(createdResult);
                var createdTodo = createdResult.Value as Todo;
                Assert.NotNull(createdTodo);
                Assert.True(createdTodo.Id > 0);
                Assert.Equal(1, createdTodo.Version);
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
                    IsCompleted = false,
                    Version = 1
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
                    IsCompleted = true,
                    Version = created.Version
                };

                var actionResult = controller.UpdateTodo(created.Id, request);

                Assert.IsType<OkObjectResult>(actionResult.Result);
                var ok = actionResult.Result as OkObjectResult;
                Assert.NotNull(ok);
                var updated = ok.Value as Todo;
                Assert.NotNull(updated);
                Assert.Equal(created.Id, updated.Id);
                Assert.Equal("Updated", updated.Title);
                Assert.Equal(created.Version + 1, updated.Version);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }

        [Fact]
        public void Controller_Update_ReturnsConflict_ForVersionMismatch()
        {
            var dbPath = TestHelpers.CreateTempDatabasePath();
            try
            {
                var service = new TodoService(TestHelpers.CreateConfiguration(dbPath));
                var controller = new TodoController(service, NullLogger<TodoController>.Instance);

                // Create an item
                var created = service.CreateTodo(new Todo { Title = "Concurrent", Description = "initial" });

                // Simulate another client updating the row first (increments Version)
                var otherUpdate = service.UpdateTodo(created.Id, new Todo { Title = "OtherUpdate", Description = "x", IsCompleted = false }, created.Version);
                Assert.NotNull(otherUpdate);
                Assert.Equal(created.Version + 1, otherUpdate.Version);

                // Now attempt to update with the stale version (created.Version)
                var request = new TodoApi.Models.Requests.UpdateTodoRequest
                {
                    Title = "MyUpdate",
                    Description = "attempt with stale version",
                    IsCompleted = true,
                    Version = created.Version // stale
                };

                var actionResult = controller.UpdateTodo(created.Id, request);

                // Expect a 409 Conflict
                Assert.IsType<ConflictObjectResult>(actionResult.Result);
                var conflict = actionResult.Result as ConflictObjectResult;
                Assert.NotNull(conflict);

                // Controller returns ProblemDetails in the Conflict body
                var problem = conflict.Value as ProblemDetails;
                Assert.NotNull(problem);
                Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
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

                var request = new TodoApi.Models.Requests.CreateTodoRequest { Title = "", Description = "Desc", IsCompleted = false };

                var actionResult = controller.CreateTodo(request);

                Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            }
            finally
            {
                TestHelpers.DeleteFileWithRetries(dbPath);
            }
        }
    }
}