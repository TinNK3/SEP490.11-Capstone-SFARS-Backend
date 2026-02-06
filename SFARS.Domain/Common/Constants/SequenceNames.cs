namespace SFARS.Domain.Common.Constants
{
    /// <summary>
    /// SQL Sequence names used for thread-safe code generation
    /// </summary>
    public static class SequenceNames
    {
        public const string IncidentCode = "IncidentCodeSeq";
        
        // Add more sequences here as needed
        // public const string RescueMissionCode = "RescueMissionCodeSeq";

        /// <summary>
        /// All valid sequence names for validation
        /// </summary>
        public static readonly HashSet<string> ValidSequences = new()
        {
            IncidentCode
        };

        public static bool IsValid(string sequenceName) => ValidSequences.Contains(sequenceName);
    }
}