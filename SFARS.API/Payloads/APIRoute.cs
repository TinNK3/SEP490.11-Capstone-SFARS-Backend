namespace SFARS.API.Payloads
{
    public class APIRoute
    {
        private const string Base = "api";

        /// <summary>
        /// Authentication endpoints
        /// </summary>
        public static class Auth
        {
            // [POST]
            public const string SignIn = Base + "/auth/sign-in";
            public const string SignInWithPassword = Base + "/auth/sign-in/password-method";
            public const string SignInWithGoogle = Base + "/auth/sign-in/google-method";
            public const string SignInWithOtp = Base + "/auth/sign-in/otp-method";
            public const string SignUp = Base + "/auth/sign-up";
            public const string RefreshToken = Base + "/auth/refresh-token";
            public const string ForgotPassword = Base + "/auth/forgot-password";
            public const string ResetPassword = Base + "/auth/reset-password";
            public const string SendOtp = Base + "/auth/otp/send";
            public const string VerifyOtp = Base + "/auth/otp/verify";
            public const string SignOut = Base + "/auth/sign-out";
            // [PUT]

        }

        /// <summary>
        /// User endpoints
        /// </summary>
        public static class User
        {
            // [GET]
            public const string Me = Base + "/me";
            public const string MeLocation = Base + "/me/location";
            // [PUT]
            public const string UpdateMe = Base + "/me";
        }

        /// <summary>
        /// Snake endpoints
        /// </summary>
        public static class Snake
        {
            // [GET]
            public const string GetAll = Base + "/snakes";
            public const string GetById = Base + "/snakes/{id}";
            public const string Search = Base + "/snakes/search";
            public const string History = Base + "/snakes/{id}/history";
            // [POST]
            public const string Create = Base + "/snakes";
            public const string ImportPreview = Base + "/snakes/import/preview";
            public const string ImportApply = Base + "/snakes/import/apply";
            // [PUT]
            public const string Update = Base + "/snakes/{id}";
            public const string Revert = Base + "/snakes/revert/{changeLogId}";
            // [PATCH]

            // [DELETE]
            public const string Delete = Base + "/snakes/{id}";
        }

        /// <summary>
        /// Incident endpoints
        /// </summary>
        public static class Incident
        {
            // [GET]
            public const string GetById = Base + "/incidents/{id}";
            public const string GetMyIncidents = Base + "/incidents/me";
            public const string Tracking = Base + "/incidents/{id}/tracking";
            public const string TrackingByCode = Base + "/tracking";
            // [POST]
            public const string Create = Base + "/incidents";
            public const string Analyze = Base + "/incidents/{id}/analyze";
            public const string RegenerateTrackingCode = Base + "/incidents/{id}/tracking-code/regenerate";
        }

        /// <summary>
        /// Medical facility endpoints
        /// </summary>
        public static class Facility
        {
            // [GET]
            public const string Nearby = Base + "/facilities/nearby";
        }

        /// <summary>
        /// General AI chatbox endpoints (RAG-based)
        /// </summary>
        public static class Chat
        {
            // [POST]
            public const string SendMessage = Base + "/chat/messages";
            // [GET]
            public const string GetSessions = Base + "/chat/sessions";
            public const string GetMessages = Base + "/chat/sessions/{id}/messages";
        }

        /// <summary>
        /// Admin — User Management endpoints
        /// </summary>
        public static class Admin
        {
            // [GET]
            public const string GetAllUsers      = Base + "/admin/users";
            public const string GetUserById      = Base + "/admin/users/{id}";
            public const string GetAuditLogs     = Base + "/admin/audit-logs";
            public const string GetUserAuditLogs = Base + "/admin/users/{id}/audit-logs";
            // [POST]
            public const string CreateUser       = Base + "/admin/users";
            // [PUT]
            public const string UpdateUserStatus = Base + "/admin/users/{id}/status";
        }

        /// <summary>
		/// System service healthcheck endpoints
		/// </summary>
		public static class HealthCheck
        {
            //	[GET]
            public const string BaseUrl = Base;
            public const string Check = Base + "/health-check";
        }
    }
}