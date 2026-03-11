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
            public const string SosPreCheck = Base + "/sos-pre-check";
            // [POST]
            public const string Create = Base + "/incidents";
            public const string Analyze = Base + "/incidents/{id}/analyze";
            public const string AiReview = Base + "/incidents/{id}/ai-review";
            public const string RegenerateTrackingCode = Base + "/incidents/{id}/tracking-code/regenerate";
            // [PATCH]
            public const string Cancel = Base + "/incidents/{id}/cancel";
        }

        /// <summary>
        /// Mission endpoints
        /// </summary>
        public static class Mission
        {
            // [POST]
            public const string Accept = Base + "/missions/{id}/accept";
            // [PATCH]
            public const string UpdateStatus = Base + "/missions/{id}/status";
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
        /// FAQ endpoints (Public - Anonymous access)
        /// </summary>
        public static class Faq
        {
            // [GET]
            public const string GetAll = Base + "/faqs";
            public const string GetById = Base + "/faqs/{id}";
        }

        /// <summary>
        /// Admin — User Management endpoints
        /// </summary>
        public static class Admin
        {
            // [GET]
            public const string GetAllUsers = Base + "/admin/users";
            public const string GetUserById = Base + "/admin/users/{id}";
            public const string GetAuditLogs = Base + "/admin/audit-logs";
            public const string GetUserAuditLogs = Base + "/admin/users/{id}/audit-logs";
            // [POST]
            public const string CreateUser = Base + "/admin/users";
            // [PUT]
            public const string UpdateUserStatus = Base + "/admin/users/{id}/status";

            // FAQ Management
            // [GET]
            public const string GetAllFaqs = Base + "/admin/faqs";
            public const string GetFaqById = Base + "/admin/faqs/{id}";
            // [POST]
            public const string CreateFaq = Base + "/admin/faqs";
            // [PUT]
            public const string UpdateFaq = Base + "/admin/faqs/{id}";
            // [DELETE]
            public const string DeleteFaq = Base + "/admin/faqs/{id}";
        }

        /// <summary>
        /// Rescuer profile endpoints
        /// </summary>
        public static class Rescuer
        {
            // [GET]
            public const string GetProfile = Base + "/rescuer/profile";
            // [PUT]
            public const string UpdateProfile = Base + "/rescuer/profile";
        }

        /// <summary>
        /// Admin — Facility Management endpoints
        /// </summary>
        public static class AdminFacility
        {
            // [GET]
            public const string GetAll     = Base + "/admin/facilities";
            public const string GetById    = Base + "/admin/facilities/{id}";
            // [POST]
            public const string Create     = Base + "/admin/facilities";
            // [PUT]
            public const string Update     = Base + "/admin/facilities/{id}";
            public const string Antivenom  = Base + "/admin/facilities/{id}/antivenom";
            public const string Deactivate = Base + "/admin/facilities/{id}/deactivate";
            public const string Activate   = Base + "/admin/facilities/{id}/activate";
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

        /// <summary>
        /// Device management endpoints
        /// </summary>
        public static class Device
        {
            // [POST]
            public const string RegisterToken = Base + "/device/token";
            // [DELETE]
            public const string UnregisterToken = Base + "/device/token/{token}";
        }

        /// <summary>
        /// Webhook endpoints — called by external providers, no auth.
        /// Signature validation handled by SmsSignatureMiddleware.
        /// </summary>
        public static class Webhook
        {
            // [POST]
            public const string SmsSOSInbound = Base + "/webhooks/sms/sos";
        }
    }
}