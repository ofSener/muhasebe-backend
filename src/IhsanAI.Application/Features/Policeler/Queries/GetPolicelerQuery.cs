using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Policeler.Dtos;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Policeler.Queries;

public record GetPolicelerQuery : IRequest<PoliceListDto>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? PoliceTuruId { get; init; }
    public int? SigortaSirketiId { get; init; }
    public int? UyeId { get; init; }
    public string? Search { get; init; }
    public int? OnayDurumu { get; init; }
}

public class GetPolicelerQueryHandler : IRequestHandler<GetPolicelerQuery, PoliceListDto>
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

    public async Task<PoliceListDto> Handle(GetPolicelerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Policeler
            .AsQueryable()
            .ApplyAuthorizationFilters(_currentUserService);

        // Date range filter (TanzimTarihi'ne göre)
        if (request.StartDate.HasValue)
        {
            var startDate = request.StartDate.Value.Date;
            query = query.Where(x => x.TanzimTarihi >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDate = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.TanzimTarihi <= endDate);
        }

        // Police Turu filter
        if (request.PoliceTuruId.HasValue)
        {
            query = query.Where(x => x.PoliceTuruId == request.PoliceTuruId.Value);
        }

        // Sigorta Sirketi filter
        if (request.SigortaSirketiId.HasValue)
        {
            query = query.Where(x => x.SigortaSirketiId == request.SigortaSirketiId.Value);
        }

        // Uye filter
        if (request.UyeId.HasValue)
        {
            query = query.Where(x => x.UyeId == request.UyeId.Value);
        }

        // Onay Durumu filter
        if (request.OnayDurumu.HasValue)
        {
            query = query.Where(x => x.OnayDurumu == request.OnayDurumu.Value);
        }

        // Search filter (PoliceNumarasi, Plaka, SigortaliAdi)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(x =>
                x.PoliceNumarasi.ToLower().Contains(searchLower) ||
                (x.Plaka != null && x.Plaka.ToLower().Contains(searchLower)) ||
                (x.SigortaliAdi != null && x.SigortaliAdi.ToLower().Contains(searchLower)));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Get paginated items
        var items = await query
            .OrderByDescending(x => x.EklenmeTarihi)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Load lookup dictionaries
        var sigortaSirketiIds = items.Select(x => x.SigortaSirketiId).Distinct().ToList();
        var sigortaSirketleriList = await _context.SigortaSirketleri
            .Where(x => sigortaSirketiIds.Contains(x.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Hem Id hem IdEski ile lookup dictionary oluştur (eski sistemle uyumluluk)
        var sigortaSirketleri = new Dictionary<int, string>();
        foreach (var s in sigortaSirketleriList)
        {
            if (!sigortaSirketleri.ContainsKey(s.Id))
                sigortaSirketleri[s.Id] = s.Ad;
            if (s.IdEski.HasValue && !sigortaSirketleri.ContainsKey(s.IdEski.Value))
                sigortaSirketleri[s.IdEski.Value] = s.Ad;
        }

        var policeTuruIds = items.Select(x => x.PoliceTuruId).Distinct().ToList();
        var policeTurleri = await _context.PoliceTurleri
            .Where(x => policeTuruIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Turu, cancellationToken);

        // Map to DTO
        var itemDtos = items.Select(item => new PoliceListItemDto
        {
            Id = item.Id,
            SigortaSirketiId = item.SigortaSirketiId,
            SigortaSirketiAdi = sigortaSirketleri.TryGetValue(item.SigortaSirketiId, out var sirket) ? sirket : null,
            PoliceTuruId = item.PoliceTuruId,
            PoliceTuruAdi = policeTurleri.TryGetValue(item.PoliceTuruId, out var tur) ? tur : null,
            PoliceNumarasi = item.PoliceNumarasi,
            Plaka = item.Plaka,
            TanzimTarihi = item.TanzimTarihi,
            BaslangicTarihi = item.BaslangicTarihi,
            BitisTarihi = item.BitisTarihi,
            BrutPrim = (decimal)item.BrutPrim,
            NetPrim = (decimal)item.NetPrim,
            SigortaliAdi = item.SigortaliAdi,
            Komisyon = item.Komisyon.HasValue ? (decimal)item.Komisyon.Value : null,
            AcenteAdi = item.AcenteAdi,
            GuncelleyenUyeId = item.GuncelleyenUyeId,
            Zeyil = item.Zeyil,
            ZeyilNo = item.ZeyilNo,
            OnayDurumu = item.OnayDurumu
        }).ToList();

        return new PoliceListDto
        {
            Items = itemDtos,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }
}

public record GetPoliceByIdQuery(int Id) : IRequest<Police?>;

public class GetPoliceByIdQueryHandler : IRequestHandler<GetPoliceByIdQuery, Police?>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Police?> Handle(GetPoliceByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Policeler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
