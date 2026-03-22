using System;

namespace SFARS.Application.Dtos.Analytics;

public class AnalyticsFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
