using System.ComponentModel.DataAnnotations;

namespace SFARS.Domain.Entities;

public class SystemConfig
{
    [Key]
    public string ConfigKey { get; set; } = null!;
    public string? ConfigValue { get; set; }
    public string? Description { get; set; }
}