using System.ComponentModel.DataAnnotations;

namespace SFARS.API.Payloads.Request.Rescuer
{
    public class ApproveAiReviewRequest
    {
        /// <summary>Optional note from rescuer</summary>
        public string? Note { get; set; }
    }

    public class RejectAiReviewRequest
    {
        /// <summary>
        /// ID of the snake the rescuer believes is the correct identification.
        /// </summary>
        [Required]
        public Guid OverrideSnakeId { get; set; }

        /// <summary>Optional reason / note</summary>
        public string? Note { get; set; }
    }
}
