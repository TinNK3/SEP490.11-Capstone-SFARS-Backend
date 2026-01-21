namespace SFARS.Application.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string name, string key) : base($"{name} ({key}) was not found")
        {
        }

        public NotFoundException(string message) : base(message)
        {
        }
    }
}
