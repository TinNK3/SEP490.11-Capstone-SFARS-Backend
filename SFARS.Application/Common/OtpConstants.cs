namespace SFARS.Application.Common
{
    /// <summary>
    /// OTP system constants for security configuration
    /// </summary>
    public static class OtpConstants
    {
        /// <summary>
        /// OTP expiration time in minutes (3 minutes)
        /// </summary>
        public const int OtpExpirationMinutes = 3;

        /// <summary>
        /// Cooldown time in seconds between OTP resend requests (60 seconds)
        /// </summary>
        public const int CooldownSeconds = 60;

        /// <summary>
        /// Maximum number of failed OTP verification attempts before lockout
        /// </summary>
        public const int MaxAttempts = 5;

        /// <summary>
        /// Lockout duration in minutes after exceeding max attempts (30 minutes)
        /// </summary>
        public const int LockoutMinutes = 30;
    }
}
