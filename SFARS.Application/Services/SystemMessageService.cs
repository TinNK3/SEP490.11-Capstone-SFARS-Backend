using MapsterMapper;
using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;

namespace SFARS.Application.Services
{
    public class SystemMessageService : ISystemMessageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<SystemMessageService> _logger;

        public SystemMessageService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<SystemMessageService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<string> GetMessageAsync(string msgId)
        {
            try
            {
                // Try to get system message from memory cache, create new if not exist
                var msgEntity = await _unitOfWork.Repository<SystemMessage, string>()
                    .GetByIdAsync(msgId);

                // Retrieve global language
                var langStr = LanguageContext.CurrentLanguage;
                var langEnum = EnumExtensions.GetValueFromDescription<SystemLanguage>(langStr);
                // Define message Language
                var message = langEnum switch
                {
                    SystemLanguage.Vietnamese => msgEntity?.Vi,
                    SystemLanguage.English => msgEntity?.En,
                    _ => msgEntity?.Vi
                };

                return message!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoke when progress get system message");
                throw new Exception("Error invoke when progress get system message");
            }
        }
    }   
}
