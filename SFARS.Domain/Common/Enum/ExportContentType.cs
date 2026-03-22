using System.ComponentModel;

namespace SFARS.Domain.Common.Enum;

/// <summary>
/// MIME content-type values for exported report files.
/// </summary>
public enum ExportContentType
{
    [Description("text/csv; charset=utf-8")]
    Csv,

    [Description("application/pdf")]
    Pdf,

    [Description("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    Excel,

    [Description("application/json")]
    Json,
}
