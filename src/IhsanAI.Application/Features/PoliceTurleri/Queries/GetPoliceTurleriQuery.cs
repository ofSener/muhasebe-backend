using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.PoliceTurleri.Queries;

public record GetPoliceTurleriQuery() : IRequest<List<PoliceTuru>>;

public class GetPoliceTurleriQueryHandler : IRequestHandler<GetPoliceTurleriQuery, List<PoliceTuru>>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceTurleriQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PoliceTuru>> Handle(GetPoliceTurleriQuery request, CancellationToken cancellationToken)
    {
        return await _context.PoliceTurleri
            .OrderBy(x => x.Turu)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetPoliceTuruByIdQuery(int Id) : IRequest<PoliceTuru?>;

public class GetPoliceTuruByIdQueryHandler : IRequestHandler<GetPoliceTuruByIdQuery, PoliceTuru?>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceTuruByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PoliceTuru?> Handle(GetPoliceTuruByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.PoliceTurleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
