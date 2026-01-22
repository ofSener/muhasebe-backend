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
}

// Query
public record GetSonAktivitelerQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 20
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

        // Poliçeleri getir
        var policeQuery = _context.Policeler.Where(p => true);
        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        // Tarih filtreleme
        if (request.StartDate.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.EklenmeTarihi >= request.StartDate.Value);
        }
        if (request.EndDate.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.EklenmeTarihi <= request.EndDate.Value);
        }

        var policeler = await policeQuery
            .OrderByDescending(p => p.EklenmeTarihi)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (policeler.Count == 0)
        {
            return new SonAktivitelerResponse { Aktiviteler = new List<SonAktiviteItem>() };
        }

        // İlişkili verileri getir
        var musteriIds = policeler.Where(p => p.MusteriId.HasValue).Select(p => p.MusteriId!.Value).Distinct().ToList();
        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var kullaniciIds = policeler.Select(p => p.IsOrtagiUyeId).Distinct().ToList();
        var bransIds = policeler.Select(p => p.BransId).Distinct().ToList();

        // Dictionary'ler ile O(1) lookup
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

        // Aktivite listesi oluştur - O(1) dictionary lookup
        var aktiviteler = policeler.Select(p =>
        {
            musteriDict.TryGetValue(p.MusteriId ?? 0, out var musteri);
            sirketDict.TryGetValue(p.SigortaSirketiId, out var sirket);
            kullaniciDict.TryGetValue(p.IsOrtagiUyeId, out var kullanici);
            bransDict.TryGetValue(p.BransId, out var brans);

            return new SonAktiviteItem
            {
                Id = p.Id,
                PoliceNo = p.PoliceNo,
                PoliceTipi = p.PoliceTipi,
                MusteriAdi = musteri != null ? $"{musteri.Adi} {musteri.Soyadi}".Trim() : "Bilinmiyor",
                SigortaSirketi = sirket?.Ad ?? $"Şirket #{p.SigortaSirketiId}",
                SigortaSirketiKodu = sirket?.Kod ?? "",
                BransAdi = brans?.Ad ?? $"Branş #{p.BransId}",
                BrutPrim = p.BrutPrim,
                Komisyon = p.Komisyon,
                EklenmeTarihi = p.EklenmeTarihi,
                EkleyenKullanici = kullanici != null ? $"{kullanici.Adi} {kullanici.Soyadi}".Trim() : ""
            };
        }).ToList();

        return new SonAktivitelerResponse
        {
            Aktiviteler = aktiviteler
        };
    }
}
