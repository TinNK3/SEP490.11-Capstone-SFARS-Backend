namespace SFARS.Application.Common
{
    public class ResultCodeConst
    {
        #region SYS
        public const string SYS_Success0001 = "SYS.Success0001";
        public const string SYS_Success0002 = "SYS.Success0002";
        public const string SYS_Success0003 = "SYS.Success0003";
        public const string SYS_Success0004 = "SYS.Success0004";
        public const string SYS_Success0005 = "SYS.Success0005";
        public const string SYS_Success0006 = "SYS.Success0006";
        public const string SYS_Success0007 = "SYS.Success0007";
        public const string SYS_Success0008 = "SYS.Success0008";

        public const string SYS_Fail0001 = "SYS.Fail0001";
        public const string SYS_Fail0002 = "SYS.Fail0002";
        public const string SYS_Fail0003 = "SYS.Fail0003";
        public const string SYS_Fail0004 = "SYS.Fail0004";
        public const string SYS_Fail0005 = "SYS.Fail0005";
        public const string SYS_Fail0007 = "SYS.Fail0007";

        public const string SYS_Warning0001 = "SYS.Warning0001";
        public const string SYS_Warning0002 = "SYS.Warning0002";
        public const string SYS_Warning0003 = "SYS.Warning0003";
        public const string SYS_Warning0004 = "SYS.Warning0004";
        public const string SYS_Warning0007 = "SYS.Warning0007"; // Forbidden - not owner/authorized
        public const string SYS_Warning0008 = "SYS.Warning0008"; // File too large
        #endregion

        #region Auth
        public const string Auth_Success0001 = "Auth.Success0001";
        public const string Auth_Success0002 = "Auth.Success0002";
        public const string Auth_Success0003 = "Auth.Success0003";
        public const string Auth_Success0005 = "Auth.Success0005";
        public const string Auth_Success0008 = "Auth.Success0008";
        public const string Auth_Success0009 = "Auth.Success0009";

        public const string Auth_Warning0001 = "Auth.Warning0001";
        public const string Auth_Warning0002 = "Auth.Warning0002";
        public const string Auth_Warning0005 = "Auth.Warning0005";
        public const string Auth_Warning0006 = "Auth.Warning0006";
        public const string Auth_Warning0007 = "Auth.Warning0007";
        public const string Auth_Warning0008 = "Auth.Warning0008";
        public const string Auth_Warning0010 = "Auth.Warning0010";
        public const string Auth_Warning0011 = "Auth.Warning0011"; // New password same as old password
        public const string Auth_Warning0013 = "Auth.Warning0013";
        public const string Auth_Warning0014 = "Auth.Warning0014"; // OTP expired
        public const string Auth_Warning0015 = "Auth.Warning0015"; // OTP locked (exceeded max attempts)
        public const string Auth_Warning0016 = "Auth.Warning0016"; // OTP cooldown (too soon to resend)
        public const string Auth_Warning0017 = "Auth.Warning0017"; // OTP not found / invalid

        public const string Auth_Fail0002 = "Auth.Fail0002";

        public const string Auth_Success0010 = "Auth.Success0010"; // OTP verified successfully
        public const string Auth_Success0011 = "Auth.Success0011"; // Please continue with OTP sign-in
        #endregion

        #region User
        public const string User_Success0001 = "User.Success0001";
        public const string User_Success0002 = "User.Success0002";

        public const string User_Warning0001 = "User.Warning0001";
        public const string User_Warning0002 = "User.Warning0002";
        #endregion

        #region Rescuer
        public const string Rescuer_Success0001 = "Rescuer.Success0001";
        public const string Rescuer_Success0002 = "Rescuer.Success0002";

        public const string Rescuer_Warning0001 = "Rescuer.Warning0001";
        public const string Rescuer_Warning0002 = "Rescuer.Warning0002";
        #endregion

        #region Snake
        public const string Snake_Success0001 = "Snake.Success0001";

        public const string Snake_Warning0001 = "Snake.Warning0001";
        #endregion

        #region Medical
        public const string Medical_Success0001 = "Medical.Success0001"; // Facility found
        public const string Medical_Success0002 = "Medical.Success0002"; // Facility created
        public const string Medical_Success0003 = "Medical.Success0003"; // Facility updated
        public const string Medical_Success0004 = "Medical.Success0004"; // Facility antivenom updated
        public const string Medical_Success0005 = "Medical.Success0005"; // Facility deactivated
        public const string Medical_Success0006 = "Medical.Success0006"; // Facility activated

        public const string Medical_Warning0001 = "Medical.Warning0001"; // No facility found nearby
        public const string Medical_Warning0002 = "Medical.Warning0002"; // Antivenom stock empty
        public const string Medical_Warning0003 = "Medical.Warning0003"; // Facility already deactivated
        public const string Medical_Warning0004 = "Medical.Warning0004"; // Facility already active
        public const string Medical_Warning0005 = "Medical.Warning0005"; // Possible duplicate facility
        public const string Medical_Warning0006 = "Medical.Warning0006"; // Location required (lat/lng not provided)
        #endregion

        #region Incident
        public const string Incident_Success0001 = "Incident.Success0001";
        public const string Incident_Success0002 = "Incident.Success0002";
        public const string Incident_Success0003 = "Incident.Success0003";
        public const string Incident_Success0004 = "Incident.Success0004";
        public const string Incident_Success0005 = "Incident.Success0005"; // SOS cancelled during grace period
        public const string Incident_Success0006 = "Incident.Success0006"; // SOS resolved via Fallback (115)
        public const string Incident_Success0007 = "Incident.Success0007"; // Voice symptom uploaded

        public const string Incident_Warning0001 = "Incident.Warning0001";
        public const string Incident_Warning0002 = "Incident.Warning0002";
        public const string Incident_Warning0003 = "Incident.Warning0003";
        public const string Incident_Warning0004 = "Incident.Warning0004"; // SOS creation blocked by spam guard
        public const string Incident_Warning0005 = "Incident.Warning0005"; // AI analysis not yet completed
        public const string Incident_Warning0006 = "Incident.Warning0006"; // Grace period expired / SOS dispatched
        public const string Incident_Warning0007 = "Incident.Warning0007"; // Incident already claimed by another rescuer
        public const string Incident_Warning0008 = "Incident.Warning0008"; // Rescuer is too far from incident
        public const string Incident_Warning0009 = "Incident.Warning0009"; // Cannot resolve incident unless Unassigned

        public const string Incident_Fail0001 = "Incident.Fail0001";

        // Notification / audit messages
        public const string Incident_Notify0001 = "Incident.Notify0001";
        public const string Incident_Notify0002 = "Incident.Notify0002";
        public const string Incident_Notify0003 = "Incident.Notify0003"; // Rescuer accepted — notify victim
        public const string Incident_Notify0004 = "Incident.Notify0004"; // Rescuer arrived — notify victim
        public const string Incident_Notify0005 = "Incident.Notify0005"; // System timeout fallback to victim
        public const string Mission_Notify0002 = "Mission.Notify0002"; // System timeout fallback to rescuer
        public const string Dispatch_Notify0003 = "Dispatch.Notify0003"; // System fallback to victim
        public const string Incident_Reason0001 = "Incident.Reason0001";  // Incident created by user
        public const string Incident_Reason0002 = "Incident.Reason0002";  // Victim cancelled during grace period
        public const string Incident_Reason0003 = "Incident.Reason0003";  // Rescuer accepted mission
        public const string Incident_Reason0004 = "Incident.Reason0004";  // Rescuer arrived at scene
        public const string Incident_Reason0005 = "Incident.Reason0005";  // Incident closed by rescuer
        public const string Incident_Reason0006 = "Incident.Reason0006";  // Claim timeout — rescuer ghosted
        public const string Incident_Reason0007 = "Incident.Reason0007";  // Dispatch fallback — no rescuer found
        public const string Incident_Reason0008 = "Incident.Reason0008";  // Victim resolved external (115)
        public const string Incident_Reason0009 = "Incident.Reason0009";  // Victim manually dispatched SOS
        public const string Incident_Reason0010 = "Incident.Reason0010";  // System auto-closed abandoned incident
        public const string Incident_Reason0011 = "Incident.Reason0011";  // Watchdog stagnation detected (VN)

        // Symptom tracking
        public const string Incident_Success0008 = "Incident.Success0008"; // Symptom update saved
        public const string Incident_Warning0010 = "Incident.Warning0010"; // Cannot update symptoms on closed/cancelled
        public const string Incident_Notify0006  = "Incident.Notify0006";  // Symptom updated — notify rescuer
        #endregion

        #region AI
        public const string AI_Success0001 = "AI.Success0001";
        public const string AI_Success0002 = "AI.Success0002";

        public const string AI_Warning0001 = "AI.Warning0001";
        public const string AI_Warning0002 = "AI.Warning0002";
        public const string AI_Warning0003 = "AI.Warning0003";
        public const string AI_Warning0004 = "AI.Warning0004";   // Invalid image type for analysis
        public const string AI_Warning0005 = "AI.Warning0005";   // Snake not found in DB
        public const string AI_Warning0006 = "AI.Warning0006";   // First-aid not available for toxin group

        #region AiReview
        public const string AiReview_Success0001 = "AiReview.Success0001";
        public const string AiReview_Fail0001 = "AiReview.Fail0001";
        public const string AiReview_Fail0002 = "AiReview.Fail0002";
        public const string AiReview_Fail0003 = "AiReview.Fail0003";
        public const string AiReview_Fail0004 = "AiReview.Fail0004";
        public const string AiReview_Warning_Pending = "AiReview.Warning.Pending";
        #endregion
        #endregion

        #region Chat
        public const string Chat_Success0001 = "Chat.Success0001";   // Message sent
        public const string Chat_Warning0001 = "Chat.Warning0001";   // Session ended
        public const string Chat_Fail0001 = "Chat.Fail0001";         // AI unavailable
        public const string Chat_Guard0001 = "Chat.Guard0001";       // Guardrail fallback
        #endregion

        #region Mission
        public const string Mission_Success0001 = "Mission.Success0001"; // Mission status updated successfully
        public const string Mission_Warning0001 = "Mission.Warning0001"; // Incident closed/cancelled, cannot update
        #endregion

        #region Dispatch
        public const string Dispatch_Success0001 = "Dispatch.Success0001"; // Dispatch chain started
        public const string Dispatch_Warning0001 = "Dispatch.Warning0001"; // No rescuer within 20 km (fail-fast)
        public const string Dispatch_Warning0002 = "Dispatch.Warning0002"; // All tiers exhausted
        public const string Dispatch_Notify0001  = "Dispatch.Notify0001";  // New SOS pushed to rescuer
        public const string Dispatch_Notify0002  = "Dispatch.Notify0002";  // Fallback: no rescuer found
        #endregion

        #region Payment
        public const string Payment_Success0001 = "Payment.Success0001"; // Transaction created
        public const string Payment_Success0002 = "Payment.Success0002"; // Webhook processed
        public const string Payment_Success0003 = "Payment.Success0003"; // Transaction cancelled

        public const string Payment_Warning0001 = "Payment.Warning0001"; // Invalid webhook signature
        public const string Payment_Warning0002 = "Payment.Warning0002"; // Transaction not found
        public const string Payment_Warning0003 = "Payment.Warning0003"; // Already processed (idempotent)
        public const string Payment_Warning0004 = "Payment.Warning0004"; // Amount validation failed
        #endregion

        #region Comm
        public const string Comm_Success0001 = "Comm.Success0001";
        public const string Comm_Success0002 = "Comm.Success0002";
        public const string Comm_Success0003 = "Comm.Success0003";

        public const string Comm_Warning0001 = "Comm.Warning0001";
        public const string Comm_Warning0002 = "Comm.Warning0002";
        #endregion

        #region Trans
        public const string Trans_Success0001 = "Trans.Success0001";
        public const string Trans_Success0002 = "Trans.Success0002";
        public const string Trans_Success0003 = "Trans.Success0003";

        public const string Trans_Warning0001 = "Trans.Warning0001";
        public const string Trans_Warning0002 = "Trans.Warning0002";

        public const string Trans_Fail0001 = "Trans.Fail0001";
        #endregion

        #region Admin
        public const string Admin_Success0001 = "Admin.Success0001"; // User list retrieved
        public const string Admin_Success0002 = "Admin.Success0002"; // User status updated
        public const string Admin_Success0003 = "Admin.Success0003"; // Admin account created
        public const string Admin_Success0004 = "Admin.Success0004"; // Rescuer account created

        public const string Admin_Warning0001 = "Admin.Warning0001"; // User not found
        public const string Admin_Warning0002 = "Admin.Warning0002"; // Cannot modify own account
        public const string Admin_Warning0003 = "Admin.Warning0003"; // Cannot remove last admin
        public const string Admin_Warning0004 = "Admin.Warning0004"; // Invalid status transition

        public const string Admin_Fail0001 = "Admin.Fail0001";       // Status transition invalid
        #endregion

        #region Analytics
        public const string Analytics_Success0001 = "Analytics.Success0001"; // Heatmap hotspot ping sent to community
        public const string Analytics_Warning0001 = "Analytics.Warning0001"; // No users with device tokens found – ping skipped
        #endregion
        // AI Voice Messages (Symptom Tracking)
        public const string Voice_BreathingDifficulty = "Voice.BreathingDifficulty";
        public const string Voice_Ptosis = "Voice.Ptosis";
        public const string Voice_Bleeding = "Voice.Bleeding";
        public const string Voice_VomitingDizziness = "Voice.VomitingDizziness";
        public const string Voice_SwellingPain = "Voice.SwellingPain";
        public const string Voice_None = "Voice.None";
    }
}