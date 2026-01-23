using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Extensions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Policeler.Queries;

public record GetPolicelerQuery(int? IsOrtagiFirmaId = null, int? Limit = 100) : IRequest<List<Police>>;

public class GetPolicelerQueryHandler : IRequestHandler<GetPolicelerQuery, List<Police>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPolicelerQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<Police>> Handle(GetPolicelerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Policeler.AsQueryable();

        // GÜVENLİK: Token'dan gelen FirmaId ile filtrele, client'a güvenme!
        query = query.ApplyFirmaFilter(_currentUserService, x => x.IsOrtagiFirmaId);

        // Şube bazlı filtreleme (GorebilecegiPoliceler = "2" ise)
        query = query.ApplySubeFilter(_currentUserService, x => x.IsOrtagiSubeId);

        return await query
            .OrderByDescending(x => x.EklenmeTarihi)
            .Take(request.Limit ?? 100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetPoliceByIdQuery(int Id) : IRequest<Police?>;

public class GetPoliceByIdQueryHandler : IRequestHandler<GetPoliceByIdQuery, Police?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPoliceByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Police?> Handle(GetPoliceByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Policeler.AsQueryable();

        // GÜVENLİK: Token'dan gelen FirmaId ile filtrele
        query = query.ApplyFirmaFilter(_currentUserService, x => x.IsOrtagiFirmaId);

        return await query
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
