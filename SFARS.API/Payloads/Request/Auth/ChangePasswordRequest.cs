namespace SFARS.API.Payloads.Request.Auth
{
    /// <summary>
    /// Request to change password (step 3 of password change flow)
    /// Requires: User authenticated + OTP verified in previous steps
    /// </summary>
    public class ChangePasswordRequest
    {
        /// <summary>
        /// Current password (for verification)
        /// </summary>
        public string CurrentPassword { get; set; } = null!;

        /// <summary>
        /// New password
        /// </summary>
        public string NewPassword { get; set; } = null!;

        /// <summary>
        /// OTP from email verification (step 2: verify-otp endpoint)
        /// Used to link the OTP verification to this password change
        /// </summary>
        public string Otp { get; set; } = null!;
    }
}

