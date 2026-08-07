using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;
using TodoApi.Services;
using TodoApi.Models.Requests;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api")]
    public class TodoController : ControllerBase
    {
        private readonly ITodoService _todoService;
        public TodoController(ITodoService todoService)
        {
            _todoService = todoService;
        }

        [HttpPost("createTodo")]
        public IActionResult CreateTodo([FromBody] Todo todo)
        {
            try
            {
                var result = _todoService.CreateTodo(todo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("getTodo")]
        public IActionResult GetTodo([FromBody] GetTodoRequest request)
        {
            try
            {
                if (request.Id.HasValue)
                {
                    var todo = _todoService.GetTodoById(request.Id.Value);
                    if (todo == null)
                    {
                        return NotFound();
                    }
                    return Ok(todo);
                }
                else
                {
                    var todos = _todoService.GetAllTodos();
                    return Ok(todos);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("updateTodo")]
        public IActionResult UpdateTodo([FromBody] UpdateTodoRequest request)
        {
            try
            {
                var existingTodo = _todoService.GetTodoById(request.Id);
                if (existingTodo == null)
                {
                    return NotFound();
                }

                var todo = new Todo
                {
                    Title = request.Title,
                    Description = request.Description,
                    IsCompleted = request.IsCompleted
                };

                var result = _todoService.UpdateTodo(request.Id, todo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("deleteTodo")]
        public IActionResult DeleteTodo([FromBody] DeleteTodoRequest request)
        {
            try
            {
                var result = _todoService.DeleteTodo(request.Id);
                if (result)
                {
                    return Ok(new { message = "Todo deleted successfully" });
                }
                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
