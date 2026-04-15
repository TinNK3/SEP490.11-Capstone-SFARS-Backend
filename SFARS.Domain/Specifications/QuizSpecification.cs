using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;

namespace SFARS.Domain.Specifications;

public class QuizSpecification : BaseSpecification<Quiz>
{
    public QuizSpecification(bool activeOnly = true) 
        : base(x => !activeOnly || x.IsActive)
    {
    }

    public QuizSpecification(Guid id) : base(x => x.Id == id)
    {
        ApplyInclude(q => q.Include(x => x.QuizQuestions).ThenInclude(x => x.Options));
    }

    public static QuizSpecification WithDetails(Guid id)
    {
        return new QuizSpecification(id);
    }
}
