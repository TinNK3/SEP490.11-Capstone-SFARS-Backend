using Microsoft.AspNetCore.Http;

namespace SFARS.Application.Dtos.Snake;

public class UpdateSnakeImagesRequest
{
    public List<Guid>? KeepImageIds { get; set; } // Danh sách ID của các ảnh cũ muốn giữ lại
    public Guid? PrimaryExistingImageId { get; set; } // ID của ảnh cũ được chọn làm ảnh chính (nếu có)
    public List<IFormFile>? NewImages { get; set; } // Danh sách các file ảnh mới tải lên
    public int? PrimaryNewImageIndex { get; set; } // Index của ảnh mới (trong NewImages) được chọn làm ảnh chính (0-based)
}
