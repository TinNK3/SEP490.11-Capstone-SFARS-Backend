using Microsoft.EntityFrameworkCore;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiInference;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Services;
using SFARS.Domain.Interfaces.Services.Base;

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
    public async Task<IServiceResult> ExportTrainingDataAsync(DateTime? since = null)
    {
        // Only export verified inferences from SNAKE photos (not wound photos)
        var verifiedInferences = await (
            from ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false)
                .Include(a => a.Incident)
                .Include(a => a.IncidentMedia)
                .Include(a => a.SelectedSnake)
                .Where(a => a.Incident.CurrentAiReviewId != null)
                .Where(a => a.IncidentMedia != null && a.IncidentMedia.MediaType == MediaType.SnakePhoto)
            join r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false)
                on ai.Id equals r.AiInferenceId
            where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                  && (since == null || r.ReviewedAt >= since)
            select new { ai, r }
        ).ToListAsync();

        var result = new DataSetExportResultDto
        {
            ExportDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalSamples = verifiedInferences.Count
        };

        foreach (var item in verifiedInferences)
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

            result.Samples.Add(new DataSetTrainingSampleDto
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

        return new ServiceResult(ResultCodeConst.SYS_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001), result);
    }

    /// <inheritdoc />
    public async Task<IServiceResult> ExportWoundTrainingDataAsync(DateTime? since = null)
    {
        // Only export verified inferences from WOUND photos
        var verifiedInferences = await (
            from ai in _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false)
                .Include(a => a.Incident)
                .Include(a => a.IncidentMedia)
                .Where(a => a.Incident.CurrentAiReviewId != null)
                .Where(a => a.IncidentMedia != null && a.IncidentMedia.MediaType == MediaType.BiteWoundPhoto)
                .Where(a => a.IsSnakeBite != null) // Must have an AI prediction to compare against
            join r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false)
                on ai.Id equals r.AiInferenceId
            where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                  && (since == null || r.ReviewedAt >= since)
            select new { ai, r }
        ).ToListAsync();

        var result = new WoundDataSetExportResultDto
        {
            ExportDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalSamples = verifiedInferences.Count
        };

        foreach (var item in verifiedInferences)
        {
            var ai = item.ai;
            var review = item.r;

            // Determine ground truth label:
            // - ConfirmedCorrect → AI was right → use AI's IsSnakeBite value
            // - Corrected → AI was wrong → flip AI's IsSnakeBite value
            bool groundTruthIsSnakeBite = review.ReviewStatus == AiReviewStatus.ConfirmedCorrect
                ? ai.IsSnakeBite!.Value
                : !ai.IsSnakeBite!.Value;

            string confirmedLabel = groundTruthIsSnakeBite ? "Snake_Bite" : "Non_Snake_Bite";

            result.Samples.Add(new WoundTrainingSampleDto
            {
                InferenceId = ai.Id,
                ImageUrl = ai.IncidentMedia?.MediaUrl ?? "",
                ConfirmedLabel = confirmedLabel,
                OriginalConfidence = ai.SelectedConfidence,
                RescuerComment = review.Comment,
                VerifiedAt = review.ReviewedAt ?? DateTime.UtcNow
            });
        }

        return new ServiceResult(ResultCodeConst.SYS_Success0001,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001), result);
    }
}