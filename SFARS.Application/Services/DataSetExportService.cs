using Microsoft.EntityFrameworkCore;
using SFARS.Application.Common;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.AiInference;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Application.Services;

public class DataSetExportService : IDataSetExportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemMessageService _msgService;

    public DataSetExportService(IUnitOfWork unitOfWork, ISystemMessageService msgService)
    {
        _unitOfWork = unitOfWork;
        _msgService = msgService;
    }

    /// <inheritdoc />
    public async Task<IServiceResult> ExportTrainingDataAsync(BaseSpecParams specParams)
    {
        var query = from ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false)
                .Include(a => a.Incident)
                .Include(a => a.IncidentMedia)
                .Include(a => a.SelectedSnake)
                .Where(a => a.Incident.CurrentAiReviewId != null)
                .Where(a => a.IncidentMedia != null && a.IncidentMedia.MediaType == MediaType.SnakePhoto)
            join r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false)
                .Include(r => r.CorrectedSnake)
                on ai.Id equals r.AiInferenceId
            where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                  && r.AdminReviewerId != null
                  // Exclude reviews pointing to inactive (new/placeholder) snakes
                  && (r.ReviewStatus != AiReviewStatus.Corrected || r.CorrectedSnake == null || r.CorrectedSnake.IsActive)
                  && (specParams.CreatedFrom == null || r.ReviewedAt >= specParams.CreatedFrom)
                  && (specParams.CreatedTo == null || r.ReviewedAt <= specParams.CreatedTo)
            select new { ai, r };

        var count = await query.CountAsync();
        var pagedItems = await query
            .OrderByDescending(x => x.r.ReviewedAt)
            .Skip(specParams.GetSkip())
            .Take(specParams.GetTake())
            .ToListAsync();

        var dtos = new List<DataSetTrainingSampleDto>();
        foreach (var item in pagedItems)
        {
            var ai = item.ai;
            var review = item.r;

            string scientificName;
            ToxinGroup toxinGroup;

            if (review.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
            {
                scientificName = ai.SelectedSnake?.ScientificName ?? "Unknown";
                toxinGroup = ai.SelectedToxinGroup;
            }
            else
            {
                var correctedSnake = await _unitOfWork.Repository<Snake, Guid>()
                    .GetByIdAsync(review.CorrectedSnakeId ?? Guid.Empty);
                scientificName = correctedSnake?.ScientificName ?? "Unknown";
                toxinGroup = review.CorrectedToxinGroup ?? ToxinGroup.Unknown;
            }

            dtos.Add(new DataSetTrainingSampleDto
            {
                InferenceId = ai.Id,
                ImageUrl = ai.IncidentMedia?.MediaUrl ?? "",
                ConfirmedScientificName = scientificName,
                ConfirmedToxinGroup = toxinGroup,
                OriginalConfidence = ai.SelectedConfidence,
                RescuerComment = review.Comment,
                VerifiedAt = review.ReviewedAt ?? DateTime.UtcNow
            });
        }

        int totalPages = count == 0 ? 0 : (int)Math.Ceiling(count / (double)specParams.GetTake());
        var result = new PaginatedResultDto<DataSetTrainingSampleDto>(dtos, specParams.GetPage(), specParams.GetTake(), totalPages, count);

        return new ServiceResult(ResultCodeConst.SYS_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001), result);
    }

    /// <inheritdoc />
    public async Task<IServiceResult> ExportWoundTrainingDataAsync(BaseSpecParams specParams)
    {
        var query = from ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false)
                .Include(a => a.Incident)
                .Include(a => a.IncidentMedia)
                .Where(a => a.Incident.CurrentAiReviewId != null)
                .Where(a => a.IncidentMedia != null && a.IncidentMedia.MediaType == MediaType.BiteWoundPhoto)
                .Where(a => a.IsSnakeBite != null)
            join r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false)
                on ai.Id equals r.AiInferenceId
            where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                  && r.AdminReviewerId != null
                  && (specParams.CreatedFrom == null || r.ReviewedAt >= specParams.CreatedFrom)
                  && (specParams.CreatedTo == null || r.ReviewedAt <= specParams.CreatedTo)
            select new { ai, r };

        var count = await query.CountAsync();
        var pagedItems = await query
            .OrderByDescending(x => x.r.ReviewedAt)
            .Skip(specParams.GetSkip())
            .Take(specParams.GetTake())
            .ToListAsync();

        var dtos = new List<WoundTrainingSampleDto>();
        foreach (var item in pagedItems)
        {
            var ai = item.ai;
            var review = item.r;

            bool groundTruthIsSnakeBite = review.IsConfirmedWoundSnakeBite 
                ?? ai.IsSnakeBite!.Value;

            string confirmedLabel = groundTruthIsSnakeBite ? "Snake_Bite" : "Non_Snake_Bite";

            dtos.Add(new WoundTrainingSampleDto
            {
                InferenceId = ai.Id,
                ImageUrl = ai.IncidentMedia?.MediaUrl ?? "",
                ConfirmedLabel = confirmedLabel,
                OriginalConfidence = ai.SelectedConfidence,
                RescuerComment = review.Comment,
                VerifiedAt = review.ReviewedAt ?? DateTime.UtcNow
            });
        }

        int totalPages = count == 0 ? 0 : (int)Math.Ceiling(count / (double)specParams.GetTake());
        var result = new PaginatedResultDto<WoundTrainingSampleDto>(dtos, specParams.GetPage(), specParams.GetTake(), totalPages, count);

        return new ServiceResult(ResultCodeConst.SYS_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001), result);
    }
}