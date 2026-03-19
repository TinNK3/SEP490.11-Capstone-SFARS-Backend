using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.AiInference;

public class DataSetTrainingSampleDto
{
    public Guid InferenceId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public string ConfirmedScientificName { get; set; } = null!;
    public ToxinGroup ConfirmedToxinGroup { get; set; }
    public double? OriginalConfidence { get; set; }
    public string? RescuerComment { get; set; }
    public DateTime VerifiedAt { get; set; }
}

public class DataSetExportResultDto
{
    public int TotalSamples { get; set; }
    public string ExportDate { get; set; } = null!;
    public List<DataSetTrainingSampleDto> Samples { get; set; } = new();
}