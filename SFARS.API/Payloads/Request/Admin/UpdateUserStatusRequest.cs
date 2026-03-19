using SFARS.Domain.Common.Enum;

namespace SFARS.API.Payloads.Request.Admin
{
    /// <summary>
    /// Request payload for updating a user's status.
    /// Status is sent as a string (`active` | `inactive` | `banned` | `deleted`).
    /// </summary>
    public class UpdateUserStatusRequest
    {
        public UserStatus Status { get; set; }

        /// <summary>Optional reason — logged server-side (e.g. ban reason).</summary>
        public string? Reason { get; set; }
    }
}
