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

            // [PUT]

        }

        /// <summary>
        /// User endpoints
        /// </summary>
        public static class User
        {
            // [GET]
            public const string Me = Base + "/me";
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
            // [POST]
            public const string Create = Base + "/incidents";
            public const string UploadMedia = Base + "/incidents/{id}/media";
            public const string CreateAiInference = Base + "/incidents/{id}/ai-inferences";
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