using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TodoApi.Models;
using TodoApi.Services;
using TodoApi.Models.Requests;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/todos")]
    public class TodoController : ControllerBase
    {
        private readonly ITodoService _todoService;
        private readonly ILogger<TodoController> _logger;

        public TodoController(ITodoService todoService, ILogger<TodoController> logger)
        {
            _todoService = todoService;
            _logger = logger;
        }

        // POST /api/todos
        [HttpPost]
        public ActionResult<Todo> CreateTodo([FromBody] CreateTodoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var todo = new Todo
            {
                Title = request.Title,
                Description = request.Description ?? string.Empty,
                IsCompleted = request.IsCompleted
            };

            var result = _todoService.CreateTodo(todo);
            return CreatedAtAction(nameof(GetTodoById), new { id = result.Id }, result);
        }

        // GET /api/todos
        [HttpGet]
        public ActionResult<List<Todo>> GetAllTodos()
        {
            var todos = _todoService.GetAllTodos();
            return Ok(todos);
        }

        // GET /api/todos/{id}
        [HttpGet("{id:int}")]
        public ActionResult<Todo> GetTodoById(int id)
        {
            var todo = _todoService.GetTodoById(id);
            if (todo == null) return NotFound();
            return Ok(todo);
        }

        // PUT /api/todos/{id}
        [HttpPut("{id:int}")]
        public ActionResult<Todo> UpdateTodo(int id, [FromBody] UpdateTodoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var todo = new Todo
            {
                Title = request.Title,
                Description = request.Description,
                IsCompleted = request.IsCompleted
            };

            var updated = _todoService.UpdateTodo(id, todo);
            if (updated == null) return NotFound();

            return Ok(updated);
        }

        // DELETE /api/todos/{id}
        [HttpDelete("{id:int}")]
        public IActionResult DeleteTodo(int id)
        {
            var deleted = _todoService.DeleteTodo(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}