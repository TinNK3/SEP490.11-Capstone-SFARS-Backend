namespace SFARS.Application.Dtos.Incident;

/// <summary>
/// Result of the SOS eligibility pre-check.
/// FE calls GET /incidents/sos-pre-check before allowing the user to trigger SOS.
/// </summary>
public class SosEligibilityDto
{
    /// <summary>
    /// Whether the user is allowed to create a new SOS incident.
    /// False only when the user is hard-blocked (repeated spam).
    /// </summary>
    public bool IsEligible { get; set; }

    /// <summary>
    /// When true: user has cancelled SOS multiple times recently.
    /// FE must show a confirmation dialog BEFORE creating the incident.
    /// The user must acknowledge the warning; no extra API call is required.
    /// </summary>
    public bool RequiresWarningConfirmation { get; set; }

    /// <summary>
    /// When <see cref="IsEligible"/> is false: reason message to display.
    /// Null when eligible.
    /// </summary>
    public string? BlockReason { get; set; }
}