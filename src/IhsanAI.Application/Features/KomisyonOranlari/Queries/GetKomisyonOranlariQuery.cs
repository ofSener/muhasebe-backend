using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.KomisyonOranlari.Queries;

public record GetKomisyonOranlariQuery(int? FirmaId = null) : IRequest<List<KomisyonOrani>>;

public class GetKomisyonOranlariQueryHandler : IRequestHandler<GetKomisyonOranlariQuery, List<KomisyonOrani>>
{
    private readonly IApplicationDbContext _context;

    public GetKomisyonOranlariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<KomisyonOrani>> Handle(GetKomisyonOranlariQuery request, CancellationToken cancellationToken)
    {
        var query = _context.KomisyonOranlari.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        return await query
            .OrderByDescending(x => x.EklenmeTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetKomisyonOraniByIdQuery(int Id) : IRequest<KomisyonOrani?>;

public class GetKomisyonOraniByIdQueryHandler : IRequestHandler<GetKomisyonOraniByIdQuery, KomisyonOrani?>
{
    private readonly IApplicationDbContext _context;

    public GetKomisyonOraniByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<KomisyonOrani?> Handle(GetKomisyonOraniByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.KomisyonOranlari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
