using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using System.Text.Json;

namespace SFARS.Application.Services;

public class AiSeedService : IAiSeedService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiAiService _geminiService;
    private readonly ISystemMessageService _msgService;
    private readonly ILogger<AiSeedService> _logger;

    public AiSeedService(
        IUnitOfWork unitOfWork,
        IGeminiAiService geminiService,
        ISystemMessageService msgService,
        ILogger<AiSeedService> logger)
    {
        _unitOfWork = unitOfWork;
        _geminiService = geminiService;
        _msgService = msgService;
        _logger = logger;
    }

    private const int MaxBatchSize = 5;

    public async Task<IServiceResult> SeedSnakeEmbeddingsAsync()
    {
        _logger.LogInformation("Starting snake embedding seeding in batches...");
        var snakes = (await _unitOfWork.Repository<Snake, Guid>().GetAllAsync())
            .Where(s => s.IsActive)
            .ToList();

        var semaphore = new SemaphoreSlim(MaxBatchSize);
        var tasks = snakes.Select(async snake =>
        {
            await semaphore.WaitAsync();
            try
            {
                var textToEmbed = $"{snake.CommonName} ({snake.ScientificName}) {snake.Description} {snake.TypicalSymptoms}";
                var vector = await _geminiService.GenerateEmbeddingAsync(textToEmbed);
                
                snake.EmbeddingJson = JsonSerializer.Serialize(vector);
                snake.UpdatedAt = DateTime.UtcNow;
                _logger.LogDebug("Generated embedding for snake: {Name}", snake.CommonName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for snake {Name}", snake.CommonName);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        await _unitOfWork.SaveChangesAsync();
        
        _logger.LogInformation("Successfully seeded {Count} snake embeddings.", snakes.Count);
        return new ServiceResult(ResultCodeConst.SYS_Success0001, $"Seeded {snakes.Count} snakes.");
    }

    public async Task<IServiceResult> SeedFirstAidEmbeddingsAsync()
    {
        _logger.LogInformation("Starting first aid embedding seeding in batches...");
        var details = (await _unitOfWork.Repository<FirstAidDetail, Guid>().GetAllAsync()).ToList();

        var semaphore = new SemaphoreSlim(MaxBatchSize);
        var tasks = details.Select(async detail =>
        {
            await semaphore.WaitAsync();
            try
            {
                var textToEmbed = $"{detail.Title} {detail.ContentMarkdown}";
                var vector = await _geminiService.GenerateEmbeddingAsync(textToEmbed);
                
                detail.EmbeddingJson = JsonSerializer.Serialize(vector);
                detail.UpdatedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for first aid detail {Id}", detail.Id);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        await _unitOfWork.SaveChangesAsync();
        
        _logger.LogInformation("Successfully seeded {Count} first aid embeddings.", details.Count);
        return new ServiceResult(ResultCodeConst.SYS_Success0001, $"Seeded {details.Count} first aid details.");
    }
}