using System.Security.Claims;

namespace SFARS.API.Extension
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            return GetUserIdOrNull(user) ?? Guid.Empty;
        }

        public static Guid? GetUserIdOrNull(this ClaimsPrincipal user)
        {
            var idString = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("userId");

            return Guid.TryParse(idString, out var id) ? id : null;
        }
    }
}