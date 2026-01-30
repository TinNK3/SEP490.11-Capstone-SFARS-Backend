namespace SFARS.Application.Dtos.Role
{
    public class SystemRoleDto
    {
        public Guid Id { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}