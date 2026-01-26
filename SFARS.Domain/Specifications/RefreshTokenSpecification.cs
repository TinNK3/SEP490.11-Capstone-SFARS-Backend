using SFARS.Domain.Entities;

namespace SFARS.Domain.Specifications
{
    /// <summary>
    /// Specifications for querying RefreshToken entities
    /// </summary>
    public class RefreshTokenSpecification : BaseSpecification<RefreshToken>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public RefreshTokenSpecification() : base()
        {
            AddOrderByDescending(r => r.CreateDate);
        }

        /// <summary>
        /// Get refresh token by ID
        /// </summary>
        public RefreshTokenSpecification(int id) : base(r => r.Id == id)
        {
        }

        /// <summary>
        /// Get refresh token by user ID
        /// </summary>
        public static RefreshTokenSpecification ByUserId(Guid userId)
        {
            var spec = new RefreshTokenSpecification();
            spec.AddFilter(r => r.UserId == userId);
            return spec;
        }

        /// <summary>
        /// Get refresh token by token ID (JWT ID)
        /// </summary>
        public static RefreshTokenSpecification ByTokenId(string tokenId)
        {
            var spec = new RefreshTokenSpecification();
            spec.AddFilter(r => r.TokenId == tokenId);
            return spec;
        }

        /// <summary>
        /// Get refresh token by refresh token ID
        /// </summary>
        public static RefreshTokenSpecification ByRefreshTokenId(string refreshTokenId)
        {
            var spec = new RefreshTokenSpecification();
            spec.AddFilter(r => r.RefreshTokenId == refreshTokenId);
            return spec;
        }

        /// <summary>
        /// Get non-expired refresh tokens for a user
        /// </summary>
        public static RefreshTokenSpecification ActiveByUserId(Guid userId)
        {
            var spec = new RefreshTokenSpecification();
            spec.AddFilter(r => r.UserId == userId && r.ExpiryDate > DateTime.UtcNow);
            return spec;
        }
    }
}
