using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models.Requests
{
    /// <summary>
    /// Represents a request to update an existing Todo item.
    /// </summary>
    public class UpdateTodoRequest
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        public bool IsCompleted { get; set; }
    }
}