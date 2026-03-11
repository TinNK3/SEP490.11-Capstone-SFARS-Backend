using FluentValidation;
using SFARS.Application.Common;
using SFARS.Application.Dtos.AiReview;
using SFARS.Application.Extensions;
using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Validations.AiReview
{
    public class SubmitAiReviewRequestDtoValidator : AbstractValidator<SubmitAiReviewRequestDto>
    {
        public SubmitAiReviewRequestDtoValidator()
        {
            var langContext = LanguageContext.CurrentLanguage ?? "en";
            var langEnum = (SystemLanguage?)EnumExtensions.GetValueFromDescription<SystemLanguage>(langContext);
            var isVi = langEnum == SystemLanguage.Vietnamese;

            RuleFor(x => x.ReviewStatus)
                .IsInEnum().WithMessage(isVi ? "Trạng thái Review không hợp lệ." : "Invalid ReviewStatus.");

            // ConfirmedCorrect requires no further info (but we allow comment)
            RuleFor(x => x.CorrectedSnakeId).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
                .WithMessage(isVi ? "Trạng thái ConfirmedCorrect không được bao gồm CorrectedSnakeId." : "ConfirmedCorrect status must not include CorrectedSnakeId.");
            RuleFor(x => x.CorrectedToxinGroup).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
                .WithMessage(isVi ? "Trạng thái ConfirmedCorrect không được bao gồm CorrectedToxinGroup." : "ConfirmedCorrect status must not include CorrectedToxinGroup.");
            RuleFor(x => x.UnableToAssessReasonChoice).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.ConfirmedCorrect)
                .WithMessage(isVi ? "Trạng thái ConfirmedCorrect không được bao gồm UnableToAssessReasonChoice." : "ConfirmedCorrect status must not include UnableToAssessReasonChoice.");

            // Corrected requires CorrectedToxinGroup and clear UnableToAssessReasonChoice
            RuleFor(x => x.CorrectedToxinGroup)
                .NotNull()
                .When(x => x.ReviewStatus == AiReviewStatus.Corrected)
                .WithMessage(isVi ? "CorrectedToxinGroup là bắt buộc khi trạng thái là Corrected." : "CorrectedToxinGroup is required when ReviewStatus is Corrected.");
            RuleFor(x => x.UnableToAssessReasonChoice).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.Corrected)
                .WithMessage(isVi ? "Trạng thái Corrected không được bao gồm UnableToAssessReasonChoice." : "Corrected status must not include UnableToAssessReasonChoice.");

            // UnableToAssess requires UnableToAssessReasonChoice and clear corrected fields
            RuleFor(x => x.UnableToAssessReasonChoice)
                .NotNull()
                .When(x => x.ReviewStatus == AiReviewStatus.UnableToAssess)
                .WithMessage(isVi ? "UnableToAssessReasonChoice là bắt buộc khi trạng thái là UnableToAssess." : "UnableToAssessReasonChoice is required when ReviewStatus is UnableToAssess.");
            RuleFor(x => x.CorrectedSnakeId).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.UnableToAssess)
                .WithMessage(isVi ? "Trạng thái UnableToAssess không được bao gồm CorrectedSnakeId." : "UnableToAssess status must not include CorrectedSnakeId.");
            RuleFor(x => x.CorrectedToxinGroup).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.UnableToAssess)
                .WithMessage(isVi ? "Trạng thái UnableToAssess không được bao gồm CorrectedToxinGroup." : "UnableToAssess status must not include CorrectedToxinGroup.");
            
            // Deferred should not have any corrected or reason fields
            RuleFor(x => x.CorrectedSnakeId).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.Deferred)
                .WithMessage(isVi ? "Trạng thái Deferred không được bao gồm CorrectedSnakeId." : "Deferred status must not include CorrectedSnakeId.");
            RuleFor(x => x.CorrectedToxinGroup).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.Deferred)
                .WithMessage(isVi ? "Trạng thái Deferred không được bao gồm CorrectedToxinGroup." : "Deferred status must not include CorrectedToxinGroup.");
            RuleFor(x => x.UnableToAssessReasonChoice).Null()
                .When(x => x.ReviewStatus == AiReviewStatus.Deferred)
                .WithMessage(isVi ? "Trạng thái Deferred không được bao gồm UnableToAssessReasonChoice." : "Deferred status must not include UnableToAssessReasonChoice.");
            
            // Pending is not allowed for submission, it's only initial state
            RuleFor(x => x.ReviewStatus)
                .NotEqual(AiReviewStatus.Pending)
                .WithMessage(isVi ? "Không thể gửi đánh giá với trạng thái Pending." : "Cannot submit a review with status Pending.");
        }
    }
}