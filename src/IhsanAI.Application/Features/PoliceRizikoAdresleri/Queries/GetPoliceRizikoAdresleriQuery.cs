using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.PoliceRizikoAdresleri.Queries;

public record GetPoliceRizikoAdresleriQuery(int? PoliceId = null) : IRequest<List<PoliceRizikoAdres>>;

public class GetPoliceRizikoAdresleriQueryHandler : IRequestHandler<GetPoliceRizikoAdresleriQuery, List<PoliceRizikoAdres>>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceRizikoAdresleriQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PoliceRizikoAdres>> Handle(GetPoliceRizikoAdresleriQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PoliceRizikoAdresleri.AsQueryable();

        if (request.PoliceId.HasValue)
        {
            query = query.Where(x => x.PoliceId == request.PoliceId.Value);
        }

        return await query
            .OrderByDescending(x => x.EklenmeTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetPoliceRizikoAdresByIdQuery(int Id) : IRequest<PoliceRizikoAdres?>;

public class GetPoliceRizikoAdresByIdQueryHandler : IRequestHandler<GetPoliceRizikoAdresByIdQuery, PoliceRizikoAdres?>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceRizikoAdresByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PoliceRizikoAdres?> Handle(GetPoliceRizikoAdresByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.PoliceRizikoAdresleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
