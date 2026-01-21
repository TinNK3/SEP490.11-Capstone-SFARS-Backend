using System.Text.Json.Serialization;

namespace SFARS.Domain.Entities
{
    public class RefreshToken
    {
        // Key
        public int Id { get; set; }

        // Refresh token ID
        public string RefreshTokenId { get; set; } = null!;

        // Token ID
        public string TokenId { get; set; } = null!;

        // Creation and expiration datetime
        public DateTime CreateDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        // For specific user
        public Guid UserId { get; set; }

        // Refresh Count
        public int RefreshCount { get; set; }

        [JsonIgnore]
        public User User { get; set; } = null!;
    }
}