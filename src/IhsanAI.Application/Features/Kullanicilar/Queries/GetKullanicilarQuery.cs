using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Kullanicilar.Queries;

public record GetKullanicilarQuery(int? FirmaId = null, int? Limit = 100) : IRequest<List<Kullanici>>;

public class GetKullanicilarQueryHandler : IRequestHandler<GetKullanicilarQuery, List<Kullanici>>
{
    private readonly IApplicationDbContext _context;

    public GetKullanicilarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Kullanici>> Handle(GetKullanicilarQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Kullanicilar.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        return await query
            .OrderByDescending(x => x.KayitTarihi)
            .Take(request.Limit ?? 100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetKullaniciByIdQuery(int Id) : IRequest<Kullanici?>;

public class GetKullaniciByIdQueryHandler : IRequestHandler<GetKullaniciByIdQuery, Kullanici?>
{
    private readonly IApplicationDbContext _context;

    public GetKullaniciByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Kullanici?> Handle(GetKullaniciByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Kullanicilar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
