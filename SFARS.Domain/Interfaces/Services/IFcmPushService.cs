namespace SFARS.Domain.Interfaces.Services;

public interface IFcmPushService
{
    Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string body, IDictionary<string, string>? data = null);
    Task SendToUserAsync(Guid userId, string title, string body, IDictionary<string, string>? data = null);
}