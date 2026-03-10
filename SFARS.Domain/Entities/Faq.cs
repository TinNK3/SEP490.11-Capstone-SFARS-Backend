using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities;

public class Faq : BaseEntity
{
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}
