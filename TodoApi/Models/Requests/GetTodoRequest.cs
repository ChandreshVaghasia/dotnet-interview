namespace TodoApi.Models.Requests
{
    /// <summary>
    /// Represents a request to retrieve a specific Todo item.
    /// </summary>
    public class GetTodoRequest
    {
        public int? Id { get; set; }
    }
}