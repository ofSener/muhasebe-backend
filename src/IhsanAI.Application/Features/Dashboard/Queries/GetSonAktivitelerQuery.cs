using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record SonAktiviteItem
{
    public int Id { get; init; }
    public string PoliceNo { get; init; } = string.Empty;
    public string PoliceTipi { get; init; } = string.Empty;
    public string MusteriAdi { get; init; } = string.Empty;
    public string SigortaSirketi { get; init; } = string.Empty;
    public string SigortaSirketiKodu { get; init; } = string.Empty;
    public string BransAdi { get; init; } = string.Empty;
    public decimal BrutPrim { get; init; }
    public decimal Komisyon { get; init; }
    public DateTime EklenmeTarihi { get; init; }
    public string EkleyenKullanici { get; init; } = string.Empty;
}

public record SonAktivitelerResponse
{
    public List<SonAktiviteItem> Aktiviteler { get; init; } = new();
    public DashboardMode Mode { get; init; }
}

// Query
public record GetSonAktivitelerQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 20,
    DashboardMode Mode = DashboardMode.Onayli,
    DashboardFilters? Filters = null
) : IRequest<SonAktivitelerResponse>;

// Handler
public class GetSonAktivitelerQueryHandler : IRequestHandler<GetSonAktivitelerQuery, SonAktivitelerResponse>
{
    private const int MinLimit = 1;
    private const int MaxLimit = 100;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSonAktivitelerQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SonAktivitelerResponse> Handle(GetSonAktivitelerQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var limit = Math.Clamp(request.Limit, MinLimit, MaxLimit);
        var filters = request.Filters ?? new DashboardFilters();

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaAktiviteler(firmaId, request.StartDate, request.EndDate, limit, filters, cancellationToken);
        }

        return await GetOnayliAktiviteler(firmaId, request.StartDate, request.EndDate, limit, filters, cancellationToken);
    }

    private async Task<SonAktivitelerResponse> GetOnayliAktiviteler(
        int? firmaId,
        DateTime? startDate,
        DateTime? endDate,
        int limit,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var policeQuery = _context.Policeler.Where(p => p.OnayDurumu == 1);
        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.FirmaId == firmaId.Value);
        }

        if (startDate.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.EklenmeTarihi >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.EklenmeTarihi <= endDate.Value);
        }

        // Apply filters
        if (filters.BransIds.Count > 0)
            policeQuery = policeQuery.Where(p => filters.BransIds.Contains(p.PoliceTuruId));
        if (filters.SubeIds.Count > 0)
            policeQuery = policeQuery.Where(p => filters.SubeIds.Contains(p.SubeId));
        if (filters.SirketIds.Count > 0)
            policeQuery = policeQuery.Where(p => filters.SirketIds.Contains(p.SigortaSirketiId));

        var policeler = await policeQuery
            .OrderByDescending(p => p.EklenmeTarihi)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (policeler.Count == 0)
        {
            return new SonAktivitelerResponse { Aktiviteler = new List<SonAktiviteItem>(), Mode = DashboardMode.Onayli };
        }

        // İlişkili verileri getir
        var musteriIds = policeler.Where(p => p.MusteriId.HasValue).Select(p => p.MusteriId!.Value).Distinct().ToList();
        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var kullaniciIds = policeler.Select(p => p.UyeId).Distinct().ToList();
        var bransIds = policeler.Select(p => p.PoliceTuruId).Distinct().ToList();

        var musteriDict = (await _context.Musteriler
            .Where(m => musteriIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Adi, m.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(m => m.Id);

        var sirketDict = (await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Ad, s.Kod })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(s => s.Id);

        var kullaniciDict = (await _context.Kullanicilar
            .Where(k => kullaniciIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(k => k.Id);

        var bransDict = (await _context.Branslar
            .Where(b => bransIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Ad })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(b => b.Id);

        var aktiviteler = policeler.Select(p =>
        {
            musteriDict.TryGetValue(p.MusteriId ?? 0, out var musteri);
            sirketDict.TryGetValue(p.SigortaSirketiId, out var sirket);
            kullaniciDict.TryGetValue(p.UyeId, out var kullanici);
            bransDict.TryGetValue(p.PoliceTuruId, out var brans);

            return new SonAktiviteItem
            {
                Id = p.Id,
                PoliceNo = p.PoliceNumarasi,
                PoliceTipi = p.Zeyil == 1 ? "Zeyil" : "Yeni",
                MusteriAdi = musteri != null ? $"{musteri.Adi} {musteri.Soyadi}".Trim() : "Bilinmiyor",
                SigortaSirketi = sirket?.Ad ?? $"Şirket #{p.SigortaSirketiId}",
                SigortaSirketiKodu = sirket?.Kod ?? "",
                BransAdi = brans?.Ad ?? $"Branş #{p.PoliceTuruId}",
                BrutPrim = (decimal)p.BrutPrim,
                Komisyon = (decimal)(p.Komisyon ?? 0),
                EklenmeTarihi = p.EklenmeTarihi,
                EkleyenKullanici = kullanici != null ? $"{kullanici.Adi} {kullanici.Soyadi}".Trim() : ""
            };
        }).ToList();

        return new SonAktivitelerResponse
        {
            Aktiviteler = aktiviteler,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<SonAktivitelerResponse> GetYakalamaAktiviteler(
        int? firmaId,
        DateTime? startDate,
        DateTime? endDate,
        int limit,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var yakalamaQuery = _context.YakalananPoliceler.AsQueryable();
        if (firmaId.HasValue)
        {
            yakalamaQuery = yakalamaQuery.Where(y => y.FirmaId == firmaId.Value);
        }

        if (startDate.HasValue)
        {
            yakalamaQuery = yakalamaQuery.Where(y => y.EklenmeTarihi >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            yakalamaQuery = yakalamaQuery.Where(y => y.EklenmeTarihi <= endDate.Value);
        }

        // Apply filters
        if (filters.BransIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.BransIds.Contains(y.PoliceTuru));
        if (filters.SubeIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.SubeIds.Contains(y.SubeId));
        if (filters.SirketIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.SirketIds.Contains(y.SigortaSirketi));
        if (filters.KullaniciIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.KullaniciIds.Contains(y.ProduktorId));

        var yakalananlar = await yakalamaQuery
            .OrderByDescending(y => y.EklenmeTarihi)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (yakalananlar.Count == 0)
        {
            return new SonAktivitelerResponse { Aktiviteler = new List<SonAktiviteItem>(), Mode = DashboardMode.Yakalama };
        }

        // İlişkili verileri getir
        var sirketIds = yakalananlar.Select(y => y.SigortaSirketi).Distinct().ToList();
        var kullaniciIds = yakalananlar.Select(y => y.UyeId).Distinct().ToList();
        var bransIds = yakalananlar.Select(y => y.PoliceTuru).Distinct().ToList();

        var sirketDict = (await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Ad, s.Kod })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(s => s.Id);

        var kullaniciDict = (await _context.Kullanicilar
            .Where(k => kullaniciIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(k => k.Id);

        // PoliceTuru için sigortapoliceturleri tablosunu kullan
        var policeTuruDict = (await _context.PoliceTurleri
            .Where(pt => bransIds.Contains(pt.Id))
            .Select(pt => new { pt.Id, pt.Turu })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(pt => pt.Id);

        var aktiviteler = yakalananlar.Select(y =>
        {
            sirketDict.TryGetValue(y.SigortaSirketi, out var sirket);
            kullaniciDict.TryGetValue(y.UyeId, out var kullanici);
            policeTuruDict.TryGetValue(y.PoliceTuru, out var policeTuru);

            return new SonAktiviteItem
            {
                Id = y.Id,
                PoliceNo = y.PoliceNumarasi,
                PoliceTipi = "Yakalanan",
                MusteriAdi = y.SigortaliAdi ?? "Bilinmiyor",
                SigortaSirketi = sirket?.Ad ?? $"Şirket #{y.SigortaSirketi}",
                SigortaSirketiKodu = sirket?.Kod ?? "",
                BransAdi = policeTuru?.Turu ?? $"Tür #{y.PoliceTuru}",
                BrutPrim = (decimal)y.BrutPrim,
                Komisyon = 0, // Yakalanan poliçelerde komisyon yok
                EklenmeTarihi = y.EklenmeTarihi,
                EkleyenKullanici = kullanici != null ? $"{kullanici.Adi} {kullanici.Soyadi}".Trim() : ""
            };
        }).ToList();

        return new SonAktivitelerResponse
        {
            Aktiviteler = aktiviteler,
            Mode = DashboardMode.Yakalama
        };
    }
}
