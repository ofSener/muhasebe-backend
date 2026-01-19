using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.AcenteKodlari.Queries;

public record GetAcenteKodlariQuery(int? FirmaId = null) : IRequest<List<AcenteKodu>>;

public class GetAcenteKodlariQueryHandler : IRequestHandler<GetAcenteKodlariQuery, List<AcenteKodu>>
{
    private readonly IApplicationDbContext _context;

    public GetAcenteKodlariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AcenteKodu>> Handle(GetAcenteKodlariQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AcenteKodlari.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        return await query
            .OrderBy(x => x.AcenteAdi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetAcenteKoduByIdQuery(int Id) : IRequest<AcenteKodu?>;

public class GetAcenteKoduByIdQueryHandler : IRequestHandler<GetAcenteKoduByIdQuery, AcenteKodu?>
{
    private readonly IApplicationDbContext _context;

    public GetAcenteKoduByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AcenteKodu?> Handle(GetAcenteKoduByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.AcenteKodlari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
