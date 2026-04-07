namespace SFARS.Application.Dtos
{
    public class SnakeImageDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = null!;
        public bool IsPrimary { get; set; }
    }
}
