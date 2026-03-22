using System;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Dtos.Analytics;

public class ExportResultDto
{
    public string FileName { get; set; } = null!;

    /// <summary>
    /// MIME type of the exported file. Use <see cref="ExportContentType"/> enum.
    /// Resolve the MIME string via <c>ContentType.GetDescription()</c>.
    /// </summary>
    public ExportContentType ContentType { get; set; } = ExportContentType.Csv;

    public string Content { get; set; } = null!;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

