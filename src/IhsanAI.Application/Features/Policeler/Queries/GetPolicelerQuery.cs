using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Policeler.Queries;

public record GetPolicelerQuery(int? Limit = 100) : IRequest<List<Police>>;

public class GetPolicelerQueryHandler : IRequestHandler<GetPolicelerQuery, List<Police>>
{
    private readonly IApplicationDbContext _context;

    public GetPolicelerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Police>> Handle(GetPolicelerQuery request, CancellationToken cancellationToken)
    {
        return await _context.Policeler
            .OrderByDescending(x => x.EklenmeTarihi)
            .Take(request.Limit ?? 100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetPoliceByIdQuery(int Id) : IRequest<Police?>;

public class GetPoliceByIdQueryHandler : IRequestHandler<GetPoliceByIdQuery, Police?>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Police?> Handle(GetPoliceByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Policeler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
