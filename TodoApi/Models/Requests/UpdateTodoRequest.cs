namespace TodoApi.Models.Requests
{
    /// <summary>
    /// Represents a request to update an existing Todo item.
    /// </summary>
    public class UpdateTodoRequest
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public bool IsCompleted { get; set; }
    }
}