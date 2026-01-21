using System.Text.Json.Serialization;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    public class User : BaseUser
    {
        // Key  
        public Guid UserId { get; set; }

        //Rule in the system
        public int RoleId { get; set; }

        // Mapping entities

        [JsonIgnore]
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}