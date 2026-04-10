using SFARS.Domain.Interfaces.Services.Base;

namespace SFARS.Domain.Interfaces.Services;

/// <summary>
/// Service to seed AI-related data (e.g. embeddings) for existing records.
/// </summary>
public interface IAiSeedService
{
    /// <summary>
    /// Generate and populate embeddings for all active snakes.
    /// </summary>
    Task<IServiceResult> SeedSnakeEmbeddingsAsync();

    /// <summary>
    /// Generate and populate embeddings for all first aid details.
    /// </summary>
    Task<IServiceResult> SeedFirstAidEmbeddingsAsync();
}