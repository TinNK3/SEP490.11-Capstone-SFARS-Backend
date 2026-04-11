using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Admin;

public class AdminModerationRequest
{
    [Required(ErrorMessage = "Lý do ẩn nội dung là bắt buộc")]
    public string Reason { get; set; } = null!;
}
