using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.YakalananPoliceler.Queries;

public record GetYakalananPolicelerQuery(int? FirmaId = null, int? Limit = 100) : IRequest<List<YakalananPolice>>;

public class GetYakalananPolicelerQueryHandler : IRequestHandler<GetYakalananPolicelerQuery, List<YakalananPolice>>
{
    private readonly IApplicationDbContext _context;

    public GetYakalananPolicelerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<YakalananPolice>> Handle(GetYakalananPolicelerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        return await query
            .OrderByDescending(x => x.EklenmeTarihi)
            .Take(request.Limit ?? 100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetYakalananPoliceByIdQuery(int Id) : IRequest<YakalananPolice?>;

public class GetYakalananPoliceByIdQueryHandler : IRequestHandler<GetYakalananPoliceByIdQuery, YakalananPolice?>
{
    private readonly IApplicationDbContext _context;

    public GetYakalananPoliceByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<YakalananPolice?> Handle(GetYakalananPoliceByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.YakalananPoliceler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
