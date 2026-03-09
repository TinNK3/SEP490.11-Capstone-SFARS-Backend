using SFARS.Domain.Common.Enum;

namespace SFARS.Domain.Interfaces.Services;

public interface IDeviceService
{
    Task RegisterDeviceTokenAsync(Guid userId, string token, DevicePlatform platform);
    Task UnregisterDeviceTokenAsync(Guid userId, string token);
}