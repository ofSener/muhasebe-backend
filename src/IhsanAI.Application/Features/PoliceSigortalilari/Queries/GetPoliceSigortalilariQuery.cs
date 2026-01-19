using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.PoliceSigortalilari.Queries;

public record GetPoliceSigortalilariQuery(int? PoliceId = null) : IRequest<List<PoliceSigortali>>;

public class GetPoliceSigortalilariQueryHandler : IRequestHandler<GetPoliceSigortalilariQuery, List<PoliceSigortali>>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceSigortalilariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PoliceSigortali>> Handle(GetPoliceSigortalilariQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PoliceSigortalilari.AsQueryable();

        if (request.PoliceId.HasValue)
        {
            query = query.Where(x => x.PoliceId == request.PoliceId.Value);
        }

        return await query
            .OrderByDescending(x => x.EklenmeTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetPoliceSigortaliByIdQuery(int Id) : IRequest<PoliceSigortali?>;

public class GetPoliceSigortaliByIdQueryHandler : IRequestHandler<GetPoliceSigortaliByIdQuery, PoliceSigortali?>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceSigortaliByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PoliceSigortali?> Handle(GetPoliceSigortaliByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.PoliceSigortalilari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
