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

    public async Task<IServiceResult> ExportTrainingDataAsync(DateTime? since = null)
    {
        // 1. Query verified inferences
        // We need: AiInference -> AiInferenceReview (Status: ConfirmedCorrect or Corrected) -> IncidentMedia
        var query = _unitOfWork.Repository<AiInference, Guid>().GetQueryable(tracked: false)
            .Include(ai => ai.Incident)
            .Include(ai => ai.IncidentMedia)
            .Include(ai => ai.SelectedSnake)
            .Where(ai => ai.Incident.CurrentAiReviewId != null);

        // Join with reviews to ensure we only get finalized ground truth
        var verifiedInferences = await (from ai in query
                                        join r in _unitOfWork.Repository<AiInferenceReview, Guid>().GetQueryable(tracked: false)
                                        on ai.Id equals r.AiInferenceId
                                        where (r.ReviewStatus == AiReviewStatus.ConfirmedCorrect || r.ReviewStatus == AiReviewStatus.Corrected)
                                        && (since == null || r.ReviewedAt >= since)
                                        select new { ai, r }).ToListAsync();

        var result = new DataSetExportResultDto
        {
            ExportDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalSamples = verifiedInferences.Count
        };

        foreach (var item in verifiedInferences)
        {
            var ai = item.ai;
            var review = item.r;

            // Determine the final "Ground Truth" snake and toxin group
            string scientificName;
            ToxinGroup toxinGroup;

            if (review.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
            {
                scientificName = ai.SelectedSnake?.ScientificName ?? "Unknown";
                toxinGroup = ai.SelectedToxinGroup;
            }
            else
            {
                // Corrected - Fetch the corrected snake for its scientific name
                var correctedSnake = await _unitOfWork.Repository<Snake, Guid>().GetByIdAsync(review.CorrectedSnakeId ?? Guid.Empty);
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

        return new ServiceResult(ResultCodeConst.SYS_Success0001, await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0001), result);
    }
}