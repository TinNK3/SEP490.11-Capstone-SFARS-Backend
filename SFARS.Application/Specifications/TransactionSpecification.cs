using Microsoft.EntityFrameworkCore;
using SFARS.Domain.Entities;
using SFARS.Domain.Specifications;
using SFARS.Domain.Specifications.Params;
using System.Linq.Expressions;

namespace SFARS.Application.Specifications;

public class TransactionSpecification : BaseSpecification<Transaction>
{
    // public int PageIndex { get; set; }
    // public int PageSize { get; set; }

    // public TransactionSpecification(TransactionSpecParams specParams, int pageIndex, int pageSize, Guid? userId = null)
    // : base(t => 
    //         string.IsNullOrEmpty(specParams.Search) ||
    //         (t.TransactionCode != null && t.TransactionCode.Contains(specParams.Search)) ||
    //         (t.Description != null && t.Description.Contains(specParams.Search)))
    // {
    //     PageIndex = pageIndex;
    //     PageSize = pageSize;

    //     //Enable split query to avoid cartesian explosion when including related entities
    //     EnableSplitQuery();

    //     ApplyInclude(q => q.Include(t => t.User));
        
    //     //Filter
    //     if (userId != null && userId != Guid.Empty)
    //     {
    //         AddFilter(t => t.UserId == userId);
    //     }
    //     if (specParams.Status.HasValue)
    //     {
    //         AddFilter(t => t.Status == specParams.Status.Value);
    //     }
    //     if (specParams.CreateDateRange != null && specParams.CreateDateRange.Length > 1)
    //     {
    //         var from = specParams.CreateDateRange[0];
    //         var to = specParams.CreateDateRange[1];

    //         if (from is null && to.HasValue)
    //             AddFilter(t => t.CreatedAt <= to.Value);
    //         else if (from.HasValue && to is null)
    //             AddFilter(t => t.CreatedAt >= from.Value);
    //         else if (from.HasValue && to.HasValue)
    //             AddFilter(t => t.CreatedAt.Date >= from.Value.Date && t.CreatedAt.Date <= to.Value.Date);
    //     }

    //     if(specParams.ExpiredAtRange != null && specParams.ExpiredAtRange.Length > 1)
    //     {
    //         var from = specParams.ExpiredAtRange[0];
    //         var to = specParams.ExpiredAtRange[1];

    //         if (from is null && to.HasValue)
    //             AddFilter(t => t.ExpiredAt <= to.Value);
    //         else if (from.HasValue && to is null)
    //             AddFilter(t => t.ExpiredAt >= from.Value);
    //         else if (from.HasValue && to.HasValue)
    //             AddFilter(t => t.ExpiredAt.HasValue && t.ExpiredAt.Value.Date >= from.Value.Date && t.ExpiredAt.Value.Date <= to.Value.Date);
    //     }

    //     if(specParams.CancelledAtRange != null && specParams.CancelledAtRange.Length > 1)
    //     {
    //         var from = specParams.CancelledAtRange[0];
    //         var to = specParams.CancelledAtRange[1];

    //         if (from is null && to.HasValue)
    //             AddFilter(t => t.CancelledAt <= to.Value);
    //         else if (from.HasValue && to is null)
    //             AddFilter(t => t.CancelledAt >= from.Value);
    //         else if (from.HasValue && to.HasValue)
    //             AddFilter(t => t.CancelledAt.HasValue && t.CancelledAt.Value.Date >= from.Value.Date && t.CancelledAt.Value.Date <= to.Value.Date);
    //     }

    //     if(specParams.AmountRange != null && specParams.AmountRange.Length > 1)
    //     {
    //         var from = specParams.AmountRange[0];
    //         var to = specParams.AmountRange[1];

    //         if (from is null && to.HasValue)
    //             AddFilter(t => t.Amount <= to.Value);
    //         else if (from.HasValue && to is null)
    //             AddFilter(t => t.Amount >= from.Value);
    //         else if (from.HasValue && to.HasValue)
    //             AddFilter(t => t.Amount >= from.Value && t.Amount <= to.Value);
    //     }

    //     //Sorting
    //     if (!string.IsNullOrEmpty(specParams.Sort))
    //     {
    //         var isDescending = specParams.Sort.StartsWith("-");
    //         var sortKey = isDescending ? specParams.Sort[1..] : specParams.Sort;

    //         Expression<Func<Transaction, object>> orderByExp = sortKey.ToLower() switch
    //         {
    //             "amount" => t => t.Amount,
    //             "status" => t => t.Status,
    //             "transactiondate" => t => t.TransactionDate ?? DateTime.MinValue,
    //             "createdat" => t => t.CreatedAt,
    //             _ => t => t.CreatedAt
    //         };

    //         if (isDescending)
    //             AddOrderByDescending(orderByExp);
    //         else
    //             AddOrderBy(orderByExp);
    //     }
    //     else
    //     {
    //         AddOrderByDescending(t => t.CreatedAt);
    //     }
    // }

    public static TransactionSpecification ListForUser(Guid userId, TransactionSpecParams p)
    {
        var spec = new TransactionSpecification();
        spec.AddFilter(t => t.UserId == userId);
        ApplyFilters(spec, p);
        ApplySorting(spec, p.Sort);
        spec.ApplyPaging(p.GetTake(), p.GetSkip());
        return spec;
    }

    public static TransactionSpecification CountForUser(Guid userId, TransactionSpecParams p)
    {
        var spec = new TransactionSpecification();
        spec.AddFilter(t => t.UserId == userId);
        ApplyFilters(spec, p);
        return spec;
    }

    public static TransactionSpecification ListForAdmin(TransactionSpecParams p)
    {
        var spec = new TransactionSpecification();
        ApplyFilters(spec, p);
        ApplySorting(spec, p.Sort);
        spec.ApplyPaging(p.GetTake(), p.GetSkip());
        return spec;
    }

    public static TransactionSpecification ListForAdminNoPaging(TransactionSpecParams p)
    {
        var spec = new TransactionSpecification();
        ApplyFilters(spec, p);
        ApplySorting(spec, p.Sort);
        return spec;
    }

    public static TransactionSpecification CountForAdmin(TransactionSpecParams p)
    {
        var spec = new TransactionSpecification();
        ApplyFilters(spec, p);
        return spec;
    }

    private static void ApplyFilters(TransactionSpecification spec, TransactionSpecParams p)
    {
        if (p.Status.HasValue)
            spec.AddFilter(t => t.Status == p.Status.Value);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.Trim();
            spec.AddFilter(t =>
                (t.TransactionCode != null && t.TransactionCode.Contains(s)) ||
                (t.Description != null && t.Description.Contains(s)) ||
                (t.PaymentLinkId != null && t.PaymentLinkId.Contains(s)));
        }

        if (p.CreatedFrom.HasValue)
            spec.AddFilter(t => t.CreatedAt >= p.CreatedFrom.Value);

        if (p.CreatedTo.HasValue)
        {
            // Ensure inclusive filter by taking the end of the day
            var toDate = p.CreatedTo.Value.Date.AddDays(1).AddTicks(-1);
            spec.AddFilter(t => t.CreatedAt <= toDate);
        }
    }

    private static void ApplySorting(TransactionSpecification spec, string? sortBy)
    {
        var sort = sortBy?.Trim();
        if (string.IsNullOrEmpty(sort))
        {
            spec.AddOrderByDescending(t => t.CreatedAt);
            return;
        }

        var isDesc = sort.StartsWith('-');
        var key = (isDesc ? sort[1..] : sort).ToLowerInvariant();

        Expression<Func<Transaction, object>> selector = key switch
        {
            "amount" => t => t.Amount,
            "status" => t => t.Status,
            "transactiondate" => t => t.TransactionDate ?? DateTime.MinValue,
            "createdat" => t => t.CreatedAt,
            _ => t => t.CreatedAt
        };

        if (isDesc) spec.AddOrderByDescending(selector);
        else spec.AddOrderBy(selector);
    }
}
