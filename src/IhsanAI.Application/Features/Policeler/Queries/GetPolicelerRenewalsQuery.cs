using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Policeler.Dtos;

namespace IhsanAI.Application.Features.Policeler.Queries;

public record GetPolicelerRenewalsQuery : IRequest<PoliceListDto>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int DaysAhead { get; init; } = 30;
    public int? PoliceTuruId { get; init; }
    public int? SigortaSirketiId { get; init; }
    public string? Search { get; init; }
}

public class GetPolicelerRenewalsQueryHandler : IRequestHandler<GetPolicelerRenewalsQuery, PoliceListDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPolicelerRenewalsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PoliceListDto> Handle(GetPolicelerRenewalsQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var futureDate = today.AddDays(request.DaysAhead);

        var query = _context.Policeler
            .AsQueryable()
            .ApplyAuthorizationFilters(_currentUserService)
            // Sadece onaylı poliçeler (havuzda değil)
            .Where(x => x.OnayDurumu == 1)
            // Sadece yenilenmemiş poliçeler
            .Where(x => x.YenilemeDurumu == 0)
            // Bitiş tarihi bugünden sonra ve X gün içinde
            .Where(x => x.BitisTarihi >= today && x.BitisTarihi <= futureDate);

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
        var pageSize = Math.Clamp(request.PageSize, 1, 1000);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // Get paginated items - Order by BitisTarihi (closest first)
        var items = await query
            .OrderBy(x => x.BitisTarihi)
            .ThenByDescending(x => x.BrutPrim)
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

        // Şube lookup (hem SubeId hem ProduktorSubeId için)
        var allSubeIds = items.Select(x => x.SubeId)
            .Concat(items.Select(x => x.ProduktorSubeId))
            .Distinct()
            .ToList();
        var subeler = await _context.Subeler
            .Where(x => allSubeIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.SubeAdi, cancellationToken);

        // Kullanıcı lookup (hem UyeId hem ProduktorId için)
        var allKullaniciIds = items.Select(x => x.UyeId)
            .Concat(items.Select(x => x.ProduktorId))
            .Distinct()
            .ToList();
        var kullanicilar = await _context.Kullanicilar
            .Where(x => allKullaniciIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => $"{x.Adi} {x.Soyadi}".Trim(), cancellationToken);

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
            CepTelefonu = item.CepTelefonu?.ToString(),
            Komisyon = item.Komisyon.HasValue ? (decimal)item.Komisyon.Value : null,
            AcenteAdi = item.AcenteAdi,
            GuncelleyenUyeId = item.GuncelleyenUyeId,
            Zeyil = item.Zeyil,
            ZeyilNo = item.ZeyilNo,
            OnayDurumu = item.OnayDurumu,
            YenilemeDurumu = item.YenilemeDurumu,
            // Prodüktör bilgileri
            ProduktorId = item.ProduktorId,
            ProduktorAdi = kullanicilar.TryGetValue(item.ProduktorId, out var produktor) ? produktor : null,
            ProduktorSubeId = item.ProduktorSubeId,
            ProduktorSubeAdi = subeler.TryGetValue(item.ProduktorSubeId, out var prodSube) ? prodSube : null,
            // Şube ve Kullanıcı bilgileri
            SubeId = item.SubeId,
            SubeAdi = subeler.TryGetValue(item.SubeId, out var sube) ? sube : null,
            UyeId = item.UyeId,
            UyeAdi = kullanicilar.TryGetValue(item.UyeId, out var uye) ? uye : null
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
