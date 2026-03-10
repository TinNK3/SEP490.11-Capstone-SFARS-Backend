namespace SFARS.API.Payloads.Request.Faq
{
    /// <summary>
    /// Request payload for updating an existing FAQ
    /// </summary>
    public class UpdateFaqRequest
    {
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
        public int Order { get; set; }
        public bool IsActive { get; set; }
    }
}
