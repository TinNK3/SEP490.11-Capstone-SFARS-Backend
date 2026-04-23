using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Common.Enum;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications.Params;

namespace SFARS.Domain.Specifications;

public class RescuerTodoMissionSpecification : BaseSpecification<RescueMission>
{
    public RescuerTodoMissionSpecification(MissionSpecParams specParams, Guid rescuerId, bool isCount = false)
        : base(m => 
            m.RescuerId == rescuerId &&
            (m.Status == RescueStatus.Accepted || m.Status == RescueStatus.Arrived || m.Status == RescueStatus.Completed) &&
            m.Incident.CurrentAiReviewStatus == AiReviewStatus.Pending &&
            m.Incident.Medias.Any(x => x.MediaType == MediaType.SnakePhoto || x.MediaType == MediaType.BiteWoundPhoto))
    {
        if (!isCount)
        {
            ApplyInclude(q => q.Include(m => m.Incident)
                               .ThenInclude(i => i.Victim!));
                               
            ApplyInclude(q => q.Include(m => m.Incident)
                               .ThenInclude(i => i.Medias!));
                               
            ApplyInclude(q => q.Include(m => m.Incident)
                               .ThenInclude(i => i.CurrentAiInference!));

            AddOrderBy(m => m.Incident.CurrentStatus);

            ApplyPaging(specParams.GetTake(), specParams.GetSkip());
        }
    }
}