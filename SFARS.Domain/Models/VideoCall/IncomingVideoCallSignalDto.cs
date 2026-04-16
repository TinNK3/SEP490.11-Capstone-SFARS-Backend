namespace SFARS.Domain.Models.VideoCall;

public class IncomingVideoCallSignalDto
{
    public Guid IncidentId { get; set; }
    public string CallerName { get; set; } = string.Empty;
}

