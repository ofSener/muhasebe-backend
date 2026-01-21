using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Kazanclar.Queries;

// Response DTOs
public record MyEarningsResponse
{
    public decimal ToplamKazanc { get; init; }
    public decimal BuAyKazanc { get; init; }
    public decimal OdenenTutar { get; init; }
    public decimal BekleyenTutar { get; init; }
    public List<MonthlyEarning> AylikTrend { get; init; } = new();
    public List<EarningByType> TureGoreDagilim { get; init; } = new();
    public List<EarningDetail> Detaylar { get; init; } = new();
}

public record MonthlyEarning
{
    public string Ay { get; init; } = string.Empty;
    public decimal Tutar { get; init; }
}

public record EarningByType
{
    public string SigortaTuru { get; init; } = string.Empty;
    public decimal Tutar { get; init; }
    public int PoliceAdedi { get; init; }
}

public record EarningDetail
{
    public int PoliceId { get; init; }
    public string PoliceNo { get; init; } = string.Empty;
    public DateTime Tarih { get; init; }
    public string MusteriAdi { get; init; } = string.Empty;
    public string SigortaSirketi { get; init; } = string.Empty;
    public string SigortaTuru { get; init; } = string.Empty;
    public decimal NetPrim { get; init; }
    public decimal SirketKomisyonu { get; init; }
    public decimal KomisyonOrani { get; init; }
    public decimal Kazanc { get; init; }
    public string OdemeDurumu { get; init; } = "Bekliyor";
}

// Query
public record GetMyEarningsQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? BransId = null,
    string? OdemeDurumu = null
) : IRequest<MyEarningsResponse>;

// Handler
public class GetMyEarningsQueryHandler : IRequestHandler<GetMyEarningsQuery, MyEarningsResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

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

    public GetMyEarningsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<MyEarningsResponse> Handle(GetMyEarningsQuery request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var currentUserId))
        {
            return new MyEarningsResponse();
        }

        // Kullanıcının poliçelerini getir
        var query = _context.Policeler
            .Where(p => p.IsOrtagiUyeId == currentUserId)
            .AsQueryable();

        // Tarih filtresi
        if (request.StartDate.HasValue)
        {
            query = query.Where(p => p.EklenmeTarihi >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(p => p.EklenmeTarihi <= request.EndDate.Value.AddDays(1));
        }

        // Branş filtresi
        if (request.BransId.HasValue)
        {
            query = query.Where(p => p.BransId == request.BransId.Value);
        }

        var policeler = await query.AsNoTracking().ToListAsync(cancellationToken);

        // Sigorta şirketi bilgilerini getir
        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var sirketler = await _context.Firmalar
            .Where(f => sirketIds.Contains(f.Id))
            .Select(f => new { f.Id, f.FirmaAdi })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Müşteri bilgilerini getir
        var musteriIds = policeler.Where(p => p.MusteriId.HasValue).Select(p => p.MusteriId!.Value).Distinct().ToList();
        var musteriler = await _context.Musteriler
            .Where(m => musteriIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Adi, m.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Toplam kazanç hesapla
        var toplamKazanc = policeler.Sum(p => p.IsOrtagiKomisyon);

        // Bu ay kazanç
        var buAyBaslangic = new DateTime(_dateTimeService.Now.Year, _dateTimeService.Now.Month, 1);
        var buAyKazanc = policeler
            .Where(p => p.EklenmeTarihi >= buAyBaslangic)
            .Sum(p => p.IsOrtagiKomisyon);

        // Şimdilik tüm kazançları "Bekliyor" olarak kabul ediyoruz
        // İleride ödeme tablosu eklendiğinde bu güncellenecek
        var odenenTutar = 0m;
        var bekleyenTutar = toplamKazanc;

        // Aylık trend (son 6 ay)
        var aylikTrend = new List<MonthlyEarning>();
        for (int i = 5; i >= 0; i--)
        {
            var ay = _dateTimeService.Now.AddMonths(-i);
            var ayBaslangic = new DateTime(ay.Year, ay.Month, 1);
            var ayBitis = ayBaslangic.AddMonths(1);

            var aylikTutar = policeler
                .Where(p => p.EklenmeTarihi >= ayBaslangic && p.EklenmeTarihi < ayBitis)
                .Sum(p => p.IsOrtagiKomisyon);

            aylikTrend.Add(new MonthlyEarning
            {
                Ay = ay.ToString("MMM yy", new System.Globalization.CultureInfo("tr-TR")),
                Tutar = aylikTutar
            });
        }

        // Türe göre dağılım
        var tureGoreDagilim = policeler
            .GroupBy(p => p.BransId)
            .Select(g => new EarningByType
            {
                SigortaTuru = BransAdlari.GetValueOrDefault(g.Key, $"Branş #{g.Key}"),
                Tutar = g.Sum(p => p.IsOrtagiKomisyon),
                PoliceAdedi = g.Count()
            })
            .OrderByDescending(x => x.Tutar)
            .ToList();

        // Detaylar
        var detaylar = policeler
            .OrderByDescending(p => p.EklenmeTarihi)
            .Take(100) // Son 100 kayıt
            .Select(p =>
            {
                var sirket = sirketler.FirstOrDefault(s => s.Id == p.SigortaSirketiId);
                var musteri = p.MusteriId.HasValue
                    ? musteriler.FirstOrDefault(m => m.Id == p.MusteriId.Value)
                    : null;

                return new EarningDetail
                {
                    PoliceId = p.Id,
                    PoliceNo = p.PoliceNo,
                    Tarih = p.EklenmeTarihi,
                    MusteriAdi = musteri != null ? $"{musteri.Adi} {musteri.Soyadi}".Trim() : "Bilinmiyor",
                    SigortaSirketi = sirket?.FirmaAdi ?? $"Şirket #{p.SigortaSirketiId}",
                    SigortaTuru = BransAdlari.GetValueOrDefault(p.BransId, $"Branş #{p.BransId}"),
                    NetPrim = p.NetPrim,
                    SirketKomisyonu = p.Komisyon,
                    KomisyonOrani = p.IsOrtagiKomisyonOrani,
                    Kazanc = p.IsOrtagiKomisyon,
                    OdemeDurumu = "Bekliyor" // İleride ödeme tablosu ile güncellenecek
                };
            })
            .ToList();

        return new MyEarningsResponse
        {
            ToplamKazanc = toplamKazanc,
            BuAyKazanc = buAyKazanc,
            OdenenTutar = odenenTutar,
            BekleyenTutar = bekleyenTutar,
            AylikTrend = aylikTrend,
            TureGoreDagilim = tureGoreDagilim,
            Detaylar = detaylar
        };
    }
}
