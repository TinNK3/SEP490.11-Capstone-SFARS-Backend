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
            public const string ChangePassword = Base + "/auth/change-password";
            public const string DeleteAccount = Base + "/auth/delete-account";
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
            public const string MeDonationHistory = Base + "/me/donation-history";
            public const string RescuersMap = Base + "/rescuers/map";
            public const string RescuerDetail = Base + "/rescuers/{id}/detail";
            // [PUT]
            public const string UpdateMe = Base + "/me";
            // [PATCH]
            public const string UpdateAvatar = Base + "/me/avatar";
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
            public const string AdminGetAll = Base + "/admin/snakes";
            // [POST]
            public const string IdentifySpecies = Base + "/snakes/identify-species";
            public const string ClassifyWound = Base + "/snakes/classify-wound";
            public const string Create = Base + "/snakes";
            public const string ImportPreview = Base + "/snakes/import/preview";
            public const string ImportApply = Base + "/snakes/import/apply";
            // [PUT]
            public const string Update = Base + "/snakes/{id}";
            public const string UpdateImages = Base + "/snakes/{id}/images";
            public const string Revert = Base + "/snakes/revert/{changeLogId}";
            // [PATCH]

            // [DELETE]
            public const string Delete = Base + "/snakes/{id}";
        }

        /// <summary>
        /// Community endpoints
        /// </summary>
        public static class Community
        {
            private const string Base = "api/community";

            // [GET]
            public const string GetPosts = Base + "/posts";
            public const string GetUserContent = Base + "/user/{userId}/content";
            public const string GetPostById = Base + "/posts/{id}";
            public const string GetComments = Base + "/posts/{id}/comments";
            public const string GetSubComments = Base + "/comments/{id}/replies";

            // [POST]
            public const string CreatePost = Base + "/posts";
            public const string AddComment = Base + "/posts/{id}/comments";
            public const string ToggleLike = Base + "/posts/{id}/like";
            public const string Share = Base + "/posts/{id}/share";

            // [PUT]
            public const string UpdatePost = Base + "/posts/{id}";

            // [PATCH]
            public const string HidePost = Base + "/posts/{id}/hide";
            public const string UnhidePost = Base + "/posts/{id}/unhide";

            // [DELETE]
            public const string DeletePost = Base + "/posts/{id}";
            public const string DeleteComment = Base + "/posts/comments/{commentId}";

            private const string AdminBase = "api/admin/community";
            public const string AdminHidePost = AdminBase + "/posts/{id}/hide";
            public const string AdminUnhidePost = AdminBase + "/posts/{id}/unhide";
        }

        /// <summary>
        /// Reels endpoints (Short Video feature)
        /// </summary>
        public static class Reels
        {
            private const string Base = "api/reels";

            // [GET]
            public const string GetFeed = Base + "/feed";
            public const string GetById = Base + "/{id}";
            public const string GetComments = Base + "/{id}/comments";
            public const string GetSubComments = Base + "/comments/{id}/sub-comments";
            public const string GetByUser = Base + "/user/{userId}";

            // [POST]
            public const string Create = Base;
            public const string ToggleLike = Base + "/{id}/like";
            public const string AddComment = Base + "/{id}/comments";
            public const string Share = Base + "/{id}/share";

            // [PATCH]
            public const string Hide = Base + "/{id}/hide";

            // [DELETE]
            public const string Delete = Base + "/{id}";
            public const string DeleteComment = Base + "/comments/{id}";

            private const string AdminBase = "api/admin/reels";
            public const string AdminHide = AdminBase + "/{id}/hide";
            public const string AdminUnhide = AdminBase + "/{id}/unhide";
        }

        /// <summary>
        /// Incident endpoints
        /// </summary>
        public static class Incident
        {
            // [GET]
            public const string GetById = Base + "/incidents/{id}";
            public const string GetMyIncidents = Base + "/incidents/me";
            public const string GetRecentCommunity = Base + "/incidents/recent";
            public const string GetAllAdmin = Base + "/admin/incidents";
            public const string GetDetailAdmin = Base + "/admin/incidents/{id}";
            public const string GetStatusHistoryAdmin = Base + "/admin/incidents/{id}/status-history";
            public const string Tracking = Base + "/incidents/{id}/tracking";
            public const string TrackingByCode = Base + "/tracking";
            public const string SosPreCheck = Base + "/sos-pre-check";
            // [POST]
            public const string Create = Base + "/incidents";
            public const string Analyze = Base + "/incidents/{id}/analyze";
            public const string AiReview = Base + "/incidents/{id}/ai-review";
            public const string AdminAiReview = Base + "/admin/incidents/{id}/ai-review";
            public const string RegenerateTrackingCode = Base + "/incidents/{id}/tracking-code/regenerate";
            public const string ResolveFallback = Base + "/incidents/{id}/resolve-fallback";
            // [PATCH]
            public const string VoiceSymptom = Base + "/incidents/{id}/voice-symptom";
            public const string UpdateSymptoms = Base + "/incidents/{id}/symptoms";
            public const string GetSymptomTimeline = Base + "/incidents/{id}/symptoms/timeline";
            public const string Cancel = Base + "/incidents/{id}/cancel";
            public const string Dispatch = Base + "/incidents/{id}/dispatch";
        }

        /// <summary>
        /// Mission endpoints
        /// </summary>
        public static class Mission
        {
            // [GET]
            public const string GetMyMissions = Base + "/missions/me";
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
            public const string GetAllPublic = Base + "/facilities";
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
            // [DELETE]
            public const string DeleteSession = Base + "/chat/sessions/{id}";
        }

        /// <summary>
        /// Quiz endpoints (Game Integration)
        /// </summary>
        public static class Quiz
        {
            private const string BaseUrl = Base + "/quizzes";

            // [GET]
            public const string GetList = BaseUrl;
            public const string GetById = BaseUrl + "/{id}";
            public const string GetForGame = BaseUrl + "/{id}/play";

            // [POST]
            public const string Submit = BaseUrl + "/submit";

            // [GET]
            public const string GetMyHistory = BaseUrl + "/my-history";
            public const string GetHistoryDetail = BaseUrl + "/history/{id}";
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
        /// First Aid endpoints (Public read)
        /// </summary>
        public static class FirstAid
        {
            // [GET]
            public const string GetAllProcedures = Base + "/first-aids";
            public const string GetProcedureById = Base + "/first-aids/{id}";
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
            public const string GetAllPayments = Base + "/admin/payments";
            public const string GetPaymentOverview = Base + "/admin/payments/overview";
            // [POST]
            public const string CreateUser = Base + "/admin/users";
            // [PUT]
            public const string UpdateUserStatus = Base + "/admin/users/{id}/status";
            public const string UpdateUser = Base + "/admin/users/{id}";
            // [PATCH]
            public const string UpdateUserRole = Base + "/admin/users/{id}/role";
            // [DELETE]
            public const string DeleteUser = Base + "/admin/users/{id}";

            // FAQ Management
            // [GET]
            public const string GetAllFaqs = Base + "/admin/faqs";
            public const string GetFaqById = Base + "/admin/faqs/{id}";
            public const string GetFaqSuggestedOrder = Base + "/admin/faqs/suggested-order";
            // [POST]
            public const string CreateFaq = Base + "/admin/faqs";
            // [PUT]
            public const string UpdateFaq = Base + "/admin/faqs/{id}";
            // [DELETE]
            public const string DeleteFaq = Base + "/admin/faqs/{id}";

            // Quiz Management
            public const string GetAllQuizzes = Base + "/admin/quizzes";
            public const string CreateQuiz = Base + "/admin/quizzes";
            public const string UpdateQuiz = Base + "/admin/quizzes/{id}";
            public const string DeleteQuiz = Base + "/admin/quizzes/{id}";
            public const string GetQuizAttempts = Base + "/admin/quizzes/attempts";
            public const string GetAttemptDetail = Base + "/admin/quizzes/attempts/{id}";
            // Gemini API Key Management
            // [GET]
            public const string GetAllGeminiKeys = Base + "/admin/gemini-keys";
            // [POST]
            public const string CreateGeminiKey = Base + "/admin/gemini-keys";
            // [PATCH]
            public const string ToggleGeminiKey = Base + "/admin/gemini-keys/{id}/toggle";
            // [DELETE]
            public const string DeleteGeminiKey = Base + "/admin/gemini-keys/{id}";

            // First Aid Detail Management
            // [GET]
            public const string GetAllFirstAidDetails = Base + "/admin/first-aid-details";
            public const string GetFirstAidDetailById = Base + "/admin/first-aid-details/{id}";
            // [POST]
            public const string CreateFirstAidDetail = Base + "/admin/first-aid-details";
            // [PUT]
            public const string UpdateFirstAidDetail = Base + "/admin/first-aid-details/{id}";
            // [DELETE]
            public const string DeleteFirstAidDetail = Base + "/admin/first-aid-details/{id}";
        }

        /// <summary>
        /// Rescuer profile endpoints
        /// </summary>
        public static class Rescuer
        {
            // [GET]
            public const string GetAllPublic = Base + "/rescuers";
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
            public const string GetAll = Base + "/admin/facilities";
            public const string GetById = Base + "/admin/facilities/{id}";
            // [POST]
            public const string Create = Base + "/admin/facilities";
            // [PUT]
            public const string Update = Base + "/admin/facilities/{id}";
            public const string Antivenom = Base + "/admin/facilities/{id}/antivenom";
            public const string Deactivate = Base + "/admin/facilities/{id}/deactivate";
            public const string Activate = Base + "/admin/facilities/{id}/activate";
            // [DELETE]
            public const string Delete = Base + "/admin/facilities/{id}";
        }

        /// <summary>
        /// Admin — MLOps & AI Retraining endpoints
        /// </summary>
        public static class AdminMlops
        {
            // Snake Species Pipeline
            // [GET]
            public const string ExportData = Base + "/admin/mlops/snake/export";
            public const string ExportByKey = Base + "/admin/mlops/snake/export-by-key";
            public const string GetRetrainHistory = Base + "/admin/mlops/snake/retrain-history";
            // [POST]
            public const string TriggerRetrain = Base + "/admin/mlops/snake/trigger-retrain";

            // Wound Classification Pipeline
            // [GET]
            public const string ExportWoundData = Base + "/admin/mlops/wound/export";
            public const string ExportWoundByKey = Base + "/admin/mlops/wound/export-by-key";
            // [POST]
            public const string TriggerWoundRetrain = Base + "/admin/mlops/wound/trigger-retrain";
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
            public const string PayOs = Base + "/webhooks/payos";
        }

        /// <summary>
        /// Payment / Transaction endpoints
        /// </summary>
        public static class Payment
        {
            // [POST]
            public const string Create = Base + "/payments";
            // [GET]
            public const string GetById = Base + "/payments/{id}";
            // [PATCH]
            public const string Cancel = Base + "/payments/{id}/cancel";
        }

        /// <summary>
        /// System Analytics endpoints
        /// </summary>
        public static class Analytics
        {
            public const string Overview = Base + "/admin/analytics/overview";
            public const string Incidents = Base + "/admin/analytics/incidents";
            public const string Rescuers = Base + "/admin/analytics/rescuers";
            public const string RescuerMissionHistory = Base + "/admin/analytics/rescuers/{rescuerId}/missions";
            public const string Ai = Base + "/admin/analytics/ai";
            public const string Heatmap = Base + "/admin/analytics/heatmap";
            public const string SnakeIncidentTracking = Base + "/admin/analytics/snakes/{snakeId}/incidents";
            public const string Export = Base + "/admin/analytics/export";
            // [POST]
            public const string PingHeatmapHotspot = Base + "/admin/analytics/heatmap/ping";
        }

        /// <summary>
        /// Notification endpoints
        /// </summary>
        public static class Notifications
        {
            // [GET]
            public const string GetList = Base + "/notifications";
            public const string GetUnreadCount = Base + "/notifications/unread-count";
            // [PATCH]
            public const string MarkAsRead = Base + "/notifications/{id}/read";
            public const string MarkAllAsRead = Base + "/notifications/read-all";
        }

        /// <summary>
        /// Reporting endpoints
        /// </summary>
        public static class Reports
        {
            private const string BaseUrl = Base + "/reports";
            private const string AdminBase = Base + "/admin/reports";

            // [GET]
            public const string GetMyReports = BaseUrl + "/my";
            public const string AdminGetAll = AdminBase;

            // [POST]
            public const string Create = BaseUrl;

            // [PATCH]
            public const string AdminUpdateStatus = AdminBase + "/{id}/status";
        }

        /// <summary>
        /// Video Call endpoints
        /// </summary>
        public static class VideoCall
        {
            private const string VideoBase = Base + "/video-call";
            // [POST]
            public const string GetToken = VideoBase + "/token/{incidentId}";
            public const string Initiate = VideoBase + "/initiate/{incidentId}";
        }
    }
}