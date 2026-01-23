namespace SFARS.Domain.Common.Constants
{
    public static class SystemConfigKeys
    {
        // Rescue Configuration
        public const string MaxRescueRadiusKm = "Rescue_MaxRadiusKm";
        public const string RescueRequestTimeoutSeconds = "Rescue_RequestTimeoutSeconds";
        
        // System Operation
        public const string IsMaintenanceMode = "System_IsMaintenanceMode";
        public const string HotlinePhoneNumber = "System_HotlinePhoneNumber";
        public const string SystemEmail = "System_Email";
        
        // Snake Identification AI
        public const string AiConfidenceThreshold = "AI_ConfidenceThreshold";
        
        // Points & Rewards
        public const string PointsPerRescue = "Points_PerRescue";
        public const string PointsPerReport = "Points_PerReport";
    }
}