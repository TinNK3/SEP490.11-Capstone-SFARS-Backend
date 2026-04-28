using System;

namespace SFARS.Application.Dtos.Analytics;

public class AiAccuracyDto
{
    public string SpeciesName { get; set; } = null!;
    public int TotalInferences { get; set; }
    public double PercentageOfTotal { get; set; }
    public double AccuracyRate { get; set; }
    public double AverageConfidence { get; set; }
    public int ReviewedCases { get; set; }
    public int UnreviewedCases { get; set; }
}