using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models.Requests
{
    /// <summary>
    /// Represents a request to create a new Todo item.
    /// </summary>
    public class CreateTodoRequest
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public bool IsCompleted { get; set; }
    }
}