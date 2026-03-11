using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Mission
{
    public class UpdateMissionStatusRequestDto
    {
        public IncidentStatus NewStatus { get; set; }
    }
}