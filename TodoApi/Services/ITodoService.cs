using TodoApi.Models;

namespace TodoApi.Services
{
    /// <summary>
    /// Defines the contract for Todo operations.
    /// </summary>
    public interface ITodoService
    {
        /// <summary>
        /// Creates a new todo item.
        /// </summary>
        /// <param name="todo">Todo item to create.</param>
        /// <returns>The created todo item.</returns>
        Todo CreateTodo(Todo todo);

        /// <summary>
        /// Gets all todo items.
        /// </summary>
        /// <returns>List of todo items.</returns>
        List<Todo> GetAllTodos();

        /// <summary>
        /// Gets a todo item by its identifier.
        /// </summary>
        /// <param name="id">Todo identifier.</param>
        /// <returns>The matching todo item if found otherwise null.</returns>
        Todo? GetTodoById(int id);

        /// <summary>
        /// Updates an existing todo item using optimistic concurrency.
        /// </summary>
        /// <param name="id">Todo identifier.</param>
        /// <param name="todo">Updated todo information.</param>
        /// <param name="expectedVersion">The client observed version for concurrency check.</param>
        /// <returns>The updated todo item, or null if the item was not found.</returns>
        /// <exception cref="ConcurrencyException">Thrown when the item exists but the version does not match (conflict).</exception>
        Todo? UpdateTodo(int id, Todo todo, int expectedVersion);

        /// <summary>
        /// Deletes a todo item.
        /// </summary>
        /// <param name="id">Todo identifier.</param>
        /// <returns>True if deleted successfully otherwise false.</returns>
        bool DeleteTodo(int id);
    }
}