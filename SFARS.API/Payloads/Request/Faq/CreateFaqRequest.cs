namespace SFARS.API.Payloads.Request.Faq
{
    /// <summary>
    /// Request payload for creating a new FAQ
    /// </summary>
    public class CreateFaqRequest
    {
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
        public int Order { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }
}
