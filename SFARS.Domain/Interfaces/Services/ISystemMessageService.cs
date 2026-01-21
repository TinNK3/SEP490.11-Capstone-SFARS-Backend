namespace SFARS.Domain.Interfaces.Services
{
    public interface ISystemMessageService
    {
        Task<string> GetMessageAsync(string msgId);
    }
}