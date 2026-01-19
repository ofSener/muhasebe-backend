using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Yetkiler.Queries;

public record GetYetkilerQuery(int? FirmaId = null) : IRequest<List<Yetki>>;

public class GetYetkilerQueryHandler : IRequestHandler<GetYetkilerQuery, List<Yetki>>
{
    private readonly IApplicationDbContext _context;

    public GetYetkilerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Yetki>> Handle(GetYetkilerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Yetkiler.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        return await query
            .OrderBy(x => x.YetkiAdi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetYetkiByIdQuery(int Id) : IRequest<Yetki?>;

public class GetYetkiByIdQueryHandler : IRequestHandler<GetYetkiByIdQuery, Yetki?>
{
    private readonly IApplicationDbContext _context;

    public GetYetkiByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Yetki?> Handle(GetYetkiByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Yetkiler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}

// YetkiAdlari
public record GetYetkiAdlariQuery() : IRequest<List<YetkiAdi>>;

public class GetYetkiAdlariQueryHandler : IRequestHandler<GetYetkiAdlariQuery, List<YetkiAdi>>
{
    private readonly IApplicationDbContext _context;

    public GetYetkiAdlariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<YetkiAdi>> Handle(GetYetkiAdlariQuery request, CancellationToken cancellationToken)
    {
        return await _context.YetkiAdlari
            .OrderBy(x => x.YetkiSirasi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
