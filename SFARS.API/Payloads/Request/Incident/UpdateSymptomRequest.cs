using SFARS.Domain.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Incident;

/// <summary>
/// Payload for the Bottom Sheet symptom update.
/// First call: includes MinutesSinceBite + Symptoms.
/// Subsequent calls: MinutesSinceBite is null, only Symptoms are sent.
/// </summary>
public class UpdateSymptomRequest
{
    /// <summary>
    /// How many minutes ago the victim was bitten.
    /// Nullable — only sent on the first submission; null on subsequent updates.
    /// </summary>
    public int? MinutesSinceBite { get; set; }

    /// <summary>
    /// List of symptom ENUMs selected by the victim via button tap.
    /// E.g.: [SymptomType.Bleeding, SymptomType.Swelling]
    /// </summary>
    [Required]
    public List<SymptomType> Symptoms { get; set; } = new();
}