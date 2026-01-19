using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Firmalar.Queries;

public record GetFirmalarQuery(bool? SadeceOnaylananlar = null) : IRequest<List<Firma>>;

public class GetFirmalarQueryHandler : IRequestHandler<GetFirmalarQuery, List<Firma>>
{
    private readonly IApplicationDbContext _context;

    public GetFirmalarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Firma>> Handle(GetFirmalarQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Firmalar.AsQueryable();

        if (request.SadeceOnaylananlar == true)
        {
            query = query.Where(x => x.Onay == 1);
        }

        return await query
            .OrderBy(x => x.FirmaAdi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetFirmaByIdQuery(int Id) : IRequest<Firma?>;

public class GetFirmaByIdQueryHandler : IRequestHandler<GetFirmaByIdQuery, Firma?>
{
    private readonly IApplicationDbContext _context;

    public GetFirmaByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Firma?> Handle(GetFirmaByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Firmalar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
