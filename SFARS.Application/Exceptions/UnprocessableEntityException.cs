namespace SFARS.Application.Exceptions
{
    [Serializable]
    public class UnprocessableEntityException : Exception
    {
        public UnprocessableEntityException(string? message, IDictionary<string, string[]> errors) : base(message)
        {
            Errors = errors;
        }

        public IDictionary<string, string[]> Errors { get; set; } = null!;
    }
}