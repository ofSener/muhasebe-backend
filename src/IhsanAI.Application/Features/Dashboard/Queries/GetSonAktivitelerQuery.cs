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
    int Limit = 20
) : IRequest<SonAktivitelerResponse>;

// Handler
public class GetSonAktivitelerQueryHandler : IRequestHandler<GetSonAktivitelerQuery, SonAktivitelerResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    // Branş ID -> Branş Adı eşleştirmesi
    private static readonly Dictionary<int, string> BransAdlari = new()
    {
        { 1, "Trafik" },
        { 2, "Kasko" },
        { 3, "DASK" },
        { 4, "Konut" },
        { 5, "Sağlık" },
        { 6, "Ferdi Kaza" },
        { 7, "Seyahat" },
        { 8, "Nakliyat" },
        { 9, "İşyeri" },
        { 10, "Diğer" }
    };

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
        var limit = Math.Min(Math.Max(request.Limit, 1), 100); // 1-100 arası

        // Poliçeleri getir
        var policeQuery = _context.Policeler.AsQueryable();
        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        var policeler = await policeQuery
            .OrderByDescending(p => p.EklenmeTarihi)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // İlişkili verileri getir
        var musteriIds = policeler.Where(p => p.MusteriId.HasValue).Select(p => p.MusteriId!.Value).Distinct().ToList();
        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var kullaniciIds = policeler.Select(p => p.IsOrtagiUyeId).Distinct().ToList();

        var musteriler = await _context.Musteriler
            .Where(m => musteriIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Adi, m.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var sirketler = await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Ad, s.Kod })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var kullanicilar = await _context.Kullanicilar
            .Where(k => kullaniciIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Aktivite listesi oluştur
        var aktiviteler = policeler.Select(p =>
        {
            var musteri = p.MusteriId.HasValue
                ? musteriler.FirstOrDefault(m => m.Id == p.MusteriId.Value)
                : null;
            var sirket = sirketler.FirstOrDefault(s => s.Id == p.SigortaSirketiId);
            var kullanici = kullanicilar.FirstOrDefault(k => k.Id == p.IsOrtagiUyeId);

            return new SonAktiviteItem
            {
                Id = p.Id,
                PoliceNo = p.PoliceNo,
                PoliceTipi = p.PoliceTipi,
                MusteriAdi = musteri != null ? $"{musteri.Adi} {musteri.Soyadi}".Trim() : "Bilinmiyor",
                SigortaSirketi = sirket?.Ad ?? $"Şirket #{p.SigortaSirketiId}",
                SigortaSirketiKodu = sirket?.Kod ?? "",
                BransAdi = BransAdlari.GetValueOrDefault(p.BransId, $"Branş #{p.BransId}"),
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
