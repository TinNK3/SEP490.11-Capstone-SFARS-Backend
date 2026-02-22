namespace SFARS.Application.Dtos;

/// <summary>
/// DTO for snake change history entries.
/// </summary>
public class SnakeChangeLogDto
{
    public Guid Id { get; set; }
    public Guid SnakeId { get; set; }
    public string FieldName { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangeType { get; set; } = null!;
    public string? ChangeReason { get; set; }
    public DateTime? CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}