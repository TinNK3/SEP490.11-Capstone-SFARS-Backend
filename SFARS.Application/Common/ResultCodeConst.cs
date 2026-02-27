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
        public const string Medical_Success0001 = "Medical.Success0001";

        public const string Medical_Warning0001 = "Medical.Warning0001";
        public const string Medical_Warning0002 = "Medical.Warning0002";
        #endregion

        #region Incident
        public const string Incident_Success0001 = "Incident.Success0001";
        public const string Incident_Success0002 = "Incident.Success0002";
        public const string Incident_Success0003 = "Incident.Success0003";
        public const string Incident_Success0004 = "Incident.Success0004";

        public const string Incident_Warning0001 = "Incident.Warning0001";
        public const string Incident_Warning0002 = "Incident.Warning0002";
        public const string Incident_Warning0003 = "Incident.Warning0003";

        public const string Incident_Fail0001 = "Incident.Fail0001";

        // Notification messages
        public const string Incident_Notify0001 = "Incident.Notify0001";
        public const string Incident_Notify0002 = "Incident.Notify0002";
        public const string Incident_Reason0001 = "Incident.Reason0001";
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
    }
}