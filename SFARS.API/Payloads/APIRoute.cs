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
            // [GET]

            // [POST]
            public const string SignInWithPassword = Base + "/auth/sign-in/password-method";
            public const string SignUp = Base + "/auth/sign-up";

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
            // [POST]
            public const string Create = Base + "/snakes";
            // [PUT]
            public const string Update = Base + "/snakes/{id}";
            // [PATCH]

            // [DELETE]
            public const string Delete = Base + "/snakes/{id}";
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