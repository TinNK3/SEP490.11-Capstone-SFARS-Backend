using System.Security.Claims;

namespace SFARS.API.Extension
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var idString = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("userId");

            return Guid.TryParse(idString, out var id) ? id : Guid.Empty;
        }
    }
}