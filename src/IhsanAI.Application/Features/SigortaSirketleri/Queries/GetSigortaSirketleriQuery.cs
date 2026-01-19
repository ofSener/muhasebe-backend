using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.SigortaSirketleri.Queries;

public record GetSigortaSirketleriQuery(bool? SadeceFaal = null) : IRequest<List<SigortaSirketi>>;

public class GetSigortaSirketleriQueryHandler : IRequestHandler<GetSigortaSirketleriQuery, List<SigortaSirketi>>
{
    private readonly IApplicationDbContext _context;

    public GetSigortaSirketleriQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SigortaSirketi>> Handle(GetSigortaSirketleriQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SigortaSirketleri.AsQueryable();

        if (request.SadeceFaal == true)
        {
            query = query.Where(x => x.Faal == 1);
        }

        return await query
            .OrderBy(x => x.Ad)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetSigortaSirketiByIdQuery(int Id) : IRequest<SigortaSirketi?>;

public class GetSigortaSirketiByIdQueryHandler : IRequestHandler<GetSigortaSirketiByIdQuery, SigortaSirketi?>
{
    private readonly IApplicationDbContext _context;

    public GetSigortaSirketiByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SigortaSirketi?> Handle(GetSigortaSirketiByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.SigortaSirketleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
