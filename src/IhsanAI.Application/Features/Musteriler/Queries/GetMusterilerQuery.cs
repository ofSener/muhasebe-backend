using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Musteriler.Queries;

public record GetMusterilerQuery(int? EkleyenFirmaId = null, int? Limit = 100) : IRequest<List<Musteri>>;

public class GetMusterilerQueryHandler : IRequestHandler<GetMusterilerQuery, List<Musteri>>
{
    private readonly IApplicationDbContext _context;

    public GetMusterilerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Musteri>> Handle(GetMusterilerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Musteriler.AsQueryable();

        if (request.EkleyenFirmaId.HasValue)
        {
            query = query.Where(x => x.EkleyenFirmaId == request.EkleyenFirmaId.Value);
        }

        return await query
            .OrderByDescending(x => x.EklenmeZamani)
            .Take(request.Limit ?? 100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetMusteriByIdQuery(int Id) : IRequest<Musteri?>;

public class GetMusteriByIdQueryHandler : IRequestHandler<GetMusteriByIdQuery, Musteri?>
{
    private readonly IApplicationDbContext _context;

    public GetMusteriByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Musteri?> Handle(GetMusteriByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Musteriler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
