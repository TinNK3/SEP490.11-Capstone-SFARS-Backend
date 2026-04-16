using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.Mission;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.Mission
{
    public class UpdateMissionStatusRequestDtoValidator : AbstractValidator<UpdateMissionStatusRequestDto>
    {
        public UpdateMissionStatusRequestDtoValidator()
        {
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            RuleFor(x => x.NewStatus)
                .IsInEnum().WithMessage(isVi ? "Trạng thái Sự cố không hợp lệ." : "Invalid Incident Status.");

            // Rescuer can only update to these semantic states during an active mission
            RuleFor(x => x.NewStatus)
                .Must(status => status == IncidentStatus.Assigned || 
                                status == IncidentStatus.Arrived || 
                                status == IncidentStatus.Closed)
                .WithMessage(isVi ? "Bạn chỉ có thể cập nhật trạng thái hoạt động: Assigned, Arrived, Closed." 
                                  : "You can only update active statuses: Assigned, Arrived, Closed.");
        }
    }
}