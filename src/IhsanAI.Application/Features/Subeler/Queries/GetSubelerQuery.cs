using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Subeler.Queries;

public record GetSubelerQuery(int? FirmaId = null) : IRequest<List<Sube>>;

public class GetSubelerQueryHandler : IRequestHandler<GetSubelerQuery, List<Sube>>
{
    private readonly IApplicationDbContext _context;

    public GetSubelerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Sube>> Handle(GetSubelerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Subeler.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        return await query
            .Where(x => x.Silinmismi != 1)
            .OrderBy(x => x.SubeAdi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetSubeByIdQuery(int Id) : IRequest<Sube?>;

public class GetSubeByIdQueryHandler : IRequestHandler<GetSubeByIdQuery, Sube?>
{
    private readonly IApplicationDbContext _context;

    public GetSubeByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Sube?> Handle(GetSubeByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Subeler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
