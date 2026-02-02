using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.YakalananPoliceler.Queries;

/// <summary>
/// Yakalanan ama havuzda olmayan poliçeleri getirir (Eşleşmeyenler)
/// </summary>
public record GetYakalananNotInPoolQuery : IRequest<YakalananNotInPoolListDto>
{
    public int? FirmaId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public int? BransId { get; init; }
    public int? SigortaSirketiId { get; init; }
}

public record YakalananNotInPoolItemDto
{
    public int Id { get; init; }
    public string PoliceNo { get; init; } = string.Empty;
    public string? SigortaliAdi { get; init; }
    public string? Brans { get; init; }
    public int BransId { get; init; }
    public decimal BrutPrim { get; init; }
    public decimal NetPrim { get; init; }
    public DateTime TanzimTarihi { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public DateTime EklenmeTarihi { get; init; }
    public string? SigortaSirketi { get; init; }
    public int SigortaSirketiId { get; init; }
    public string? Plaka { get; init; }

    // Prodüktör ve Şube bilgileri
    public string? ProduktorAdi { get; init; }
    public string? SubeAdi { get; init; }

    // Havuza göndermek için gerekli alanlar
    public int FirmaId { get; init; }
    public int SubeId { get; init; }
    public int UyeId { get; init; }
    public int ProduktorId { get; init; }
    public int ProduktorSubeId { get; init; }
}

public record YakalananNotInPoolListDto
{
    public List<YakalananNotInPoolItemDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public class GetYakalananNotInPoolQueryHandler : IRequestHandler<GetYakalananNotInPoolQuery, YakalananNotInPoolListDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetYakalananNotInPoolQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<YakalananNotInPoolListDto> Handle(GetYakalananNotInPoolQuery request, CancellationToken cancellationToken)
    {
        // Yakalanan poliçeleri çek
        var query = _context.YakalananPoliceler.AsQueryable();

        // 🔒 Authorization filtresi - Firma kontrolü
        var userFirmaId = _currentUserService.FirmaId;
        if (userFirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == userFirmaId.Value);
        }
        else if (request.FirmaId.HasValue)
        {
            // Fallback: Request'ten gelen firmaId
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        // 🔒 Yetki bazlı filtreleme (gorebilecegiPoliceler)
        var gorebilecegiPoliceler = _currentUserService.GorebilecegiPoliceler ?? Domain.Constants.PermissionLevels.OwnPolicies;

        switch (gorebilecegiPoliceler)
        {
            case Domain.Constants.PermissionLevels.AllCompanyPolicies: // "1" - Admin
                // Tüm firma poliçelerini görebilir
                break;

            case Domain.Constants.PermissionLevels.BranchPolicies: // "2" - Şube
                // Sadece kendi şubesinin poliçelerini görebilir
                if (_currentUserService.SubeId.HasValue)
                {
                    query = query.Where(x => x.SubeId == _currentUserService.SubeId.Value);
                }
                break;

            case Domain.Constants.PermissionLevels.OwnPolicies: // "3" - Kendisi
                // Sadece kendine ait poliçeleri görebilir
                var userId = _currentUserService.UyeId ?? 0;
                query = query.Where(x => x.UyeId == userId);
                break;

            case Domain.Constants.PermissionLevels.NoPolicies: // "4" - Hiçbiri
                // Hiçbir poliçe göremez
                query = query.Where(x => false);
                break;

            default:
                // Varsayılan: Sadece kendine ait
                var defaultUserId = _currentUserService.UyeId ?? 0;
                query = query.Where(x => x.UyeId == defaultUserId);
                break;
        }

        // Havuzda OLMAYAN poliçeleri filtrele (Anti-join)
        query = query.Where(y => !_context.PoliceHavuzlari.Any(p =>
            p.PoliceNo == y.PoliceNumarasi &&
            p.SigortaSirketiId == y.SigortaSirketi));

        // Search filtresi
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            query = query.Where(x =>
                x.PoliceNumarasi.ToLower().Contains(searchLower) ||
                (x.Plaka != null && x.Plaka.ToLower().Contains(searchLower)) ||
                (x.SigortaliAdi != null && x.SigortaliAdi.ToLower().Contains(searchLower)));
        }

        // Branş filtresi
        if (request.BransId.HasValue)
        {
            query = query.Where(x => x.PoliceTuru == request.BransId.Value);
        }

        // Sigorta şirketi filtresi
        if (request.SigortaSirketiId.HasValue)
        {
            query = query.Where(x => x.SigortaSirketi == request.SigortaSirketiId.Value);
        }

        // Toplam sayıyı al
        var totalCount = await query.CountAsync(cancellationToken);

        // Pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(x => x.TanzimTarihi)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(y => new
            {
                y.Id,
                y.PoliceNumarasi,
                y.SigortaliAdi,
                y.PoliceTuru,
                y.SigortaSirketi,
                y.BrutPrim,
                y.NetPrim,
                y.TanzimTarihi,
                y.BaslangicTarihi,
                y.BitisTarihi,
                y.EklenmeTarihi,
                y.Plaka,
                y.FirmaId,
                y.SubeId,
                y.UyeId,
                y.ProduktorId,
                y.ProduktorSubeId
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Branş adlarını çek
        var bransIds = items.Select(i => i.PoliceTuru).Distinct().ToList();
        var branslar = await _context.PoliceTurleri
            .Where(pt => bransIds.Contains(pt.Id))
            .AsNoTracking()
            .ToDictionaryAsync(pt => pt.Id, pt => pt.Turu ?? $"Tür #{pt.Id}", cancellationToken);

        // Sigorta şirketi adlarını çek
        var sirketIds = items.Select(i => i.SigortaSirketi).Distinct().ToList();
        var sirketler = await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Ad, cancellationToken);

        // Prodüktör adlarını çek
        var produktorIds = items.Select(i => i.ProduktorId).Distinct().ToList();
        var produktorler = await _context.Kullanicilar
            .Where(k => produktorIds.Contains(k.Id))
            .AsNoTracking()
            .ToDictionaryAsync(k => k.Id, k => (k.Adi ?? "") + " " + (k.Soyadi ?? ""), cancellationToken);

        // Şube adlarını çek
        var subeIds = items.Select(i => i.SubeId).Distinct().ToList();
        var subeler = await _context.Subeler
            .Where(s => subeIds.Contains(s.Id))
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.SubeAdi, cancellationToken);

        // DTO'ya dönüştür
        var itemDtos = items.Select(item => new YakalananNotInPoolItemDto
        {
            Id = item.Id,
            PoliceNo = item.PoliceNumarasi,
            SigortaliAdi = item.SigortaliAdi,
            Brans = branslar.GetValueOrDefault(item.PoliceTuru),
            BransId = item.PoliceTuru,
            BrutPrim = (decimal)item.BrutPrim,
            NetPrim = (decimal)item.NetPrim,
            TanzimTarihi = item.TanzimTarihi,
            BaslangicTarihi = item.BaslangicTarihi,
            BitisTarihi = item.BitisTarihi,
            EklenmeTarihi = item.EklenmeTarihi,
            SigortaSirketi = sirketler.GetValueOrDefault(item.SigortaSirketi),
            SigortaSirketiId = item.SigortaSirketi,
            Plaka = item.Plaka,
            ProduktorAdi = produktorler.GetValueOrDefault(item.ProduktorId),
            SubeAdi = subeler.GetValueOrDefault(item.SubeId),
            FirmaId = item.FirmaId,
            SubeId = item.SubeId,
            UyeId = item.UyeId,
            ProduktorId = item.ProduktorId,
            ProduktorSubeId = item.ProduktorSubeId
        }).ToList();

        return new YakalananNotInPoolListDto
        {
            Items = itemDtos,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }
}
