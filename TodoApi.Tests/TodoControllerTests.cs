using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using TodoApi.Services;
using TodoApi.Models;
using TodoApi.Controllers;
using TodoApi.Models.Requests;
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
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            mockService
                .Setup(s => s.CreateTodo(It.IsAny<Todo>()))
                .Returns((Todo t) =>
                {
                    t.Id = 1;
                    t.Version = 1;
                    t.CreatedAt = DateTime.UtcNow;
                    return t;
                });

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            var request = new CreateTodoRequest { Title = "Test", Description = "Desc", IsCompleted = false };

            // Act
            var actionResult = controller.CreateTodo(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdResult.Value);
            Assert.True(createdTodo.Id > 0);
            Assert.Equal(1, createdTodo.Version);

            mockService.Verify(s => s.CreateTodo(It.IsAny<Todo>()), Times.Once);
        }

        [Fact]
        public void Controller_GetById_ReturnsOk_WhenExists()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            var id = 42;
            var sample = new Todo { Id = id, Title = "X", Description = "Y", CreatedAt = DateTime.UtcNow, Version = 1 };
            mockService.Setup(s => s.GetTodoById(id)).Returns(sample);

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            // Act
            var actionResult = controller.GetTodoById(id);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var todo = Assert.IsType<Todo>(ok.Value);
            Assert.Equal(id, todo.Id);

            mockService.Verify(s => s.GetTodoById(id), Times.Once);
        }

        [Fact]
        public void Controller_GetById_ReturnsNotFound_WhenMissing()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            mockService.Setup(s => s.GetTodoById(It.IsAny<int>())).Returns((Todo?)null);

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            // Act
            var actionResult = controller.GetTodoById(123456);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult.Result);
            mockService.Verify(s => s.GetTodoById(123456), Times.Once);
        }

        [Fact]
        public void Controller_Update_ReturnsNotFound_ForMissingId()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            mockService.Setup(s => s.UpdateTodo(It.IsAny<int>(), It.IsAny<Todo>(), It.IsAny<int>())).Returns((Todo?)null);

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            var request = new UpdateTodoRequest
            {
                Title = "Nope",
                Description = "No row",
                IsCompleted = false,
                Version = 1
            };

            // Act
            var actionResult = controller.UpdateTodo(99999, request);

            // Assert
            Assert.IsType<NotFoundResult>(actionResult.Result);
            mockService.Verify(s => s.UpdateTodo(99999, It.IsAny<Todo>(), request.Version), Times.Once);
        }

        [Fact]
        public void Controller_Update_ReturnsOk_ForExisting()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            var id = 7;
            var existingVersion = 1;
            mockService.Setup(s => s.UpdateTodo(id, It.IsAny<Todo>(), existingVersion))
                       .Returns((int i, Todo t, int v) =>
                       {
                           t.Id = i;
                           t.Version = v + 1;
                           return t;
                       });

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            var request = new UpdateTodoRequest
            {
                Title = "Updated",
                Description = "Updated Desc",
                IsCompleted = true,
                Version = existingVersion
            };

            // Act
            var actionResult = controller.UpdateTodo(id, request);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var updated = Assert.IsType<Todo>(ok.Value);
            Assert.Equal(id, updated.Id);
            Assert.Equal("Updated", updated.Title);
            Assert.Equal(existingVersion + 1, updated.Version);

            mockService.Verify(s => s.UpdateTodo(id, It.IsAny<Todo>(), existingVersion), Times.Once);
        }

        [Fact]
        public void Controller_Update_ReturnsConflict_ForVersionMismatch()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            var id = 9;
            mockService.Setup(s => s.UpdateTodo(id, It.IsAny<Todo>(), It.IsAny<int>()))
                       .Throws(new ConcurrencyException());

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            var request = new UpdateTodoRequest
            {
                Title = "MyUpdate",
                Description = "attempt with stale version",
                IsCompleted = true,
                Version = 1
            };

            // Act
            var actionResult = controller.UpdateTodo(id, request);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(actionResult.Result);
            var problem = Assert.IsType<ProblemDetails>(conflict.Value);
            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);

            mockService.Verify(s => s.UpdateTodo(id, It.IsAny<Todo>(), request.Version), Times.Once);
        }

        [Fact]
        public void Controller_Delete_ReturnsNoContent_WhenDeleted()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            var id = 5;
            mockService.Setup(s => s.DeleteTodo(id)).Returns(true);

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            // Act
            var result = controller.DeleteTodo(id);

            // Assert
            Assert.IsType<NoContentResult>(result);
            mockService.Verify(s => s.DeleteTodo(id), Times.Once);
        }

        [Fact]
        public void Controller_Delete_ReturnsNotFound_WhenMissing()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            mockService.Setup(s => s.DeleteTodo(It.IsAny<int>())).Returns(false);

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            // Act
            var result = controller.DeleteTodo(99999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
            mockService.Verify(s => s.DeleteTodo(99999), Times.Once);
        }

        [Fact]
        public void Controller_Create_ReturnsBadRequest_ForInvalidModel()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            // No setup for CreateTodo - it should not be called when model is invalid.

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);
            controller.ModelState.AddModelError("Title", "Required");

            var request = new CreateTodoRequest { Title = "", Description = "Desc", IsCompleted = false };

            // Act
            var actionResult = controller.CreateTodo(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            mockService.Verify(s => s.CreateTodo(It.IsAny<Todo>()), Times.Never);
        }

        [Fact]
        public void GetAllTodos_UsesDefaultPagination_WhenNoQueryProvided()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            var sampleItems = new List<Todo>
            {
                new Todo { Id = 1, Title = "A", CreatedAt = DateTime.UtcNow, Version = 1 }
            };
            var paged = new PaginatedResult<Todo>(sampleItems, totalCount: 1, pageNumber: 1, pageSize: 20);

            mockService.Setup(s => s.GetTodosPaged(1, 20)).Returns(paged);

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            // Act
            var actionResult = controller.GetAllTodos();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returned = Assert.IsType<PaginatedResult<Todo>>(okResult.Value);
            Assert.Equal(1, returned.TotalCount);
            Assert.Single(returned.Items);

            mockService.Verify(s => s.GetTodosPaged(1, 20), Times.Once);
        }

        [Fact]
        public void GetAllTodos_ForwardsQueryParameters_ToService()
        {
            // Arrange
            var mockService = new Mock<ITodoService>(MockBehavior.Strict);
            var sampleItems = new List<Todo>
            {
                new Todo { Id = 10, Title = "Paged", CreatedAt = DateTime.UtcNow, Version = 1 }
            };
            var paged = new PaginatedResult<Todo>(sampleItems, totalCount: 50, pageNumber: 2, pageSize: 5);

            mockService.Setup(s => s.GetTodosPaged(2, 5)).Returns(paged);

            var controller = new TodoController(mockService.Object, NullLogger<TodoController>.Instance);

            // Act
            var actionResult = controller.GetAllTodos(pageNumber: 2, pageSize: 5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returned = Assert.IsType<PaginatedResult<Todo>>(okResult.Value);
            Assert.Equal(50, returned.TotalCount);
            Assert.Equal(2, returned.PageNumber);
            Assert.Equal(5, returned.PageSize);
            Assert.Single(returned.Items);
            Assert.Equal(10, returned.Items[0].Id);

            mockService.Verify(s => s.GetTodosPaged(2, 5), Times.Once);
        }
    }
}