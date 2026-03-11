using SFARS.Application.Dtos.AiInference;

namespace SFARS.Application.Dtos.Incident
{
    public class IncidentDetailDto : IncidentDto
    {
        public List<FirstAidStepDto> FirstAidSteps { get; set; } = new();
        public List<string> Prohibitions { get; set; } = new();
    }
}