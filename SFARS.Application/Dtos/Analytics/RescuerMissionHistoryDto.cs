using System;
using System.Collections.Generic;

namespace SFARS.Application.Dtos.Analytics;

public class RescuerMissionHistoryDto
{
    public Guid? RescuerId { get; set; }
    public string? RescuerName { get; set; }
    public int TotalMissions { get; set; }
    public double SuccessRate { get; set; }
    public PaginatedResultDto<MissionDetailDto> Missions { get; set; } = null!;
}
