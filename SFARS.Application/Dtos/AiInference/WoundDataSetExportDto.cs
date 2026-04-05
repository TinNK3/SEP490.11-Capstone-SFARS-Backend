namespace SFARS.Application.Dtos.AiInference;

public class WoundTrainingSampleDto
{
    public Guid InferenceId { get; set; }
    public string ImageUrl { get; set; } = null!;

    /// <summary>
    /// Ground truth label: "Snake_Bite" or "Non_Snake_Bite".
    /// </summary>
    public string ConfirmedLabel { get; set; } = null!;

    public double? OriginalConfidence { get; set; }
    public string? RescuerComment { get; set; }
    public DateTime VerifiedAt { get; set; }
}

public class WoundDataSetExportResultDto
{
    public int TotalSamples { get; set; }
    public string ExportDate { get; set; } = null!;
    public List<WoundTrainingSampleDto> Samples { get; set; } = new();
}