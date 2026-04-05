namespace SFARS.Application.Dtos.AiInference;

public class WoundClassificationResponseDto
{
    public bool IsWoundDetected { get; set; }
    public bool IsSnakeBite { get; set; }
    public float Confidence { get; set; }
    public string Note { get; set; } = string.Empty;
}