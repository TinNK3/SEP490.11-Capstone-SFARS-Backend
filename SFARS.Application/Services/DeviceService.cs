using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Specifications;

namespace SFARS.Application.Services;

public class DeviceService : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork;

    public DeviceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task RegisterDeviceTokenAsync(Guid userId, string token, DevicePlatform platform)
    {
        var repo = _unitOfWork.Repository<UserDevice, Guid>();
        
        // Find existing token EXACTLY, regardless of who owns it currently
        var spec = new BaseSpecification<UserDevice>(d => d.DeviceToken == token);
        var existing = await repo.GetWithSpecAsync(spec);

        if (existing == null)
        {
            // Add new device token
            await repo.AddAsync(new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceToken = token,
                Platform = platform,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            });
        }
        else
        {
            if (existing.UserId != userId)
            {
                // Device changed owners (User logged out, new user logged in)
                existing.UserId = userId;
            }
            
            // Update LastLoginAt for purging
            existing.Platform = platform;
            existing.LastLoginAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            await repo.UpdateAsync(existing);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UnregisterDeviceTokenAsync(Guid userId, string token)
    {
        var repo = _unitOfWork.Repository<UserDevice, Guid>();
        
        var spec = new BaseSpecification<UserDevice>(d => d.UserId == userId && d.DeviceToken == token);
        var existing = await repo.GetWithSpecAsync(spec);

        if (existing != null)
        {
            await repo.DeleteAsync(existing.Id);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}