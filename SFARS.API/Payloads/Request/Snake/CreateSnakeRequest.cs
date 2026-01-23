namespace SFARS.API.Payloads.Request.Snake
{
    public class CreateSnakeRequest
    {
        public string CommonName { get; set; } = null!;
        public string ScientificName { get; set; } = null!;
        public string ToxicityLevel { get; set; } = null!;
        public string? Description { get; set; }
        public string? Habitat { get; set; }
    }
}