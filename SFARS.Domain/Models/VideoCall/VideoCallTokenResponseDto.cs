namespace SFARS.Domain.Models.VideoCall;

public class VideoCallTokenResponseDto
{
    public string AppId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public uint Uid { get; set; }
}
