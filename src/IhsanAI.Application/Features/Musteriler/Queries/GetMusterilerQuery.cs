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

public record MusteriSearchDto
{
    public int Id { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? Gsm { get; init; }
    public string? Email { get; init; }
}

public record SearchCustomersQuery(string Name, int? EkleyenFirmaId = null, int Limit = 20) : IRequest<List<MusteriSearchDto>>;

public class SearchCustomersQueryHandler : IRequestHandler<SearchCustomersQuery, List<MusteriSearchDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchCustomersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<MusteriSearchDto>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Musteriler.AsQueryable();

        if (request.EkleyenFirmaId.HasValue)
        {
            query = query.Where(x => x.EkleyenFirmaId == request.EkleyenFirmaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var searchTerm = request.Name.ToLower();
            query = query.Where(x =>
                (x.Adi != null && x.Adi.ToLower().Contains(searchTerm)) ||
                (x.Soyadi != null && x.Soyadi.ToLower().Contains(searchTerm)) ||
                (x.TcKimlikNo != null && x.TcKimlikNo.Contains(searchTerm)));
        }

        return await query
            .OrderBy(x => x.Adi)
            .ThenBy(x => x.Soyadi)
            .Take(request.Limit)
            .Select(x => new MusteriSearchDto
            {
                Id = x.Id,
                Adi = x.Adi,
                Soyadi = x.Soyadi,
                TcKimlikNo = x.TcKimlikNo,
                Gsm = x.Gsm,
                Email = x.Email
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
