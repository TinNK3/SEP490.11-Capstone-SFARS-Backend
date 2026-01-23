namespace SFARS.Application.Dtos
{
    public class SnakeDto
    {
        public Guid Id { get; set; }
        public string CommonName { get; set; } = null!;
        public string ScientificName { get; set; } = null!;
        public string ToxicityLevel { get; set; } = null!;
        public string? Description { get; set; }
        public string? Habitat { get; set; }
        public bool IsActive { get; set; }
    }
}