using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.PoliceHavuzlari.Queries;

public record GetPoliceHavuzlariQuery(int? IsOrtagiFirmaId = null, int? Limit = 100) : IRequest<List<PoliceHavuz>>;

public class GetPoliceHavuzlariQueryHandler : IRequestHandler<GetPoliceHavuzlariQuery, List<PoliceHavuz>>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceHavuzlariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PoliceHavuz>> Handle(GetPoliceHavuzlariQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PoliceHavuzlari.AsQueryable();

        if (request.IsOrtagiFirmaId.HasValue)
        {
            query = query.Where(x => x.IsOrtagiFirmaId == request.IsOrtagiFirmaId.Value);
        }

        return await query
            .OrderByDescending(x => x.EklenmeTarihi)
            .Take(request.Limit ?? 100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetPoliceHavuzByIdQuery(int Id) : IRequest<PoliceHavuz?>;

public class GetPoliceHavuzByIdQueryHandler : IRequestHandler<GetPoliceHavuzByIdQuery, PoliceHavuz?>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceHavuzByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PoliceHavuz?> Handle(GetPoliceHavuzByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.PoliceHavuzlari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
