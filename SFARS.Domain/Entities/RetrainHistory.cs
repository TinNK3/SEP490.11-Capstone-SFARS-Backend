using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities.Base;

namespace SFARS.Domain.Entities
{
    /// <summary>
    /// Records an execution of the AI Retrain Pipeline.
    /// Used by Admins to track MLOps progress and model versioning history.
    /// </summary>
    public class RetrainHistory : BaseEntity
    {
        public string? DatasetVersion { get; set; }
        public string? ModelVersion { get; set; }
        public int TotalSamplesProcessed { get; set; }
        
        public double? OldAccuracy { get; set; }
        public double? NewAccuracy { get; set; }
        
        /// <summary>
        /// True if the new model's accuracy beat the champion threshold and was hot-swapped into the running app.
        /// </summary>
        public bool IsPromoted { get; set; }
        
        public RetrainStatus Status { get; set; } = RetrainStatus.Pending;
        
        public string? ErrorMessage { get; set; }
        
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}