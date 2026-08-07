using System.ComponentModel.DataAnnotations;

public class Todo
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; }

    // Optimistic concurrency version
    public int Version { get; set; } = 1;
}