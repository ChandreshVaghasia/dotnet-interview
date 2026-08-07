namespace TodoApi.Models.Requests
{
    /// <summary>
    /// Represents a request to delete a Todo item.
    /// </summary>
    public class DeleteTodoRequest
    {
        public int Id { get; set; }
    }
}