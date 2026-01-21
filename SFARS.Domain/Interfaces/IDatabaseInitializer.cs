
namespace SFARS.Domain.Interfaces
{
	public interface IDatabaseInitializer
	{
		Task InitializeAsync();
		Task SeedAsync();
		Task TrySeedAsync();
	}
}