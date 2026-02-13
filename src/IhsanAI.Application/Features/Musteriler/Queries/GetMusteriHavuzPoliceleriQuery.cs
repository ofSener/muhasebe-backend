using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Extensions;

namespace IhsanAI.Application.Features.Musteriler.Queries;

public record MusteriHavuzPoliceDto
{
    public int Id { get; init; }
    public string PoliceNo { get; init; } = string.Empty;
    public string BransAdi { get; init; } = string.Empty;
    public string SirketAdi { get; init; } = string.Empty;
    public decimal BrutPrim { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public DateTime TanzimTarihi { get; init; }
    public string Plaka { get; init; } = string.Empty;
    public string PoliceTipi { get; init; } = string.Empty;
}

public record GetMusteriHavuzPoliceleriQuery(int MusteriId) : IRequest<List<MusteriHavuzPoliceDto>>;

public class GetMusteriHavuzPoliceleriQueryHandler : IRequestHandler<GetMusteriHavuzPoliceleriQuery, List<MusteriHavuzPoliceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMusteriHavuzPoliceleriQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<MusteriHavuzPoliceDto>> Handle(GetMusteriHavuzPoliceleriQuery request, CancellationToken cancellationToken)
    {
        // 1. Müşteriyi erişim kontrolü ile bul
        var musteri = await _context.Musteriler
            .AsQueryable()
            .ApplyMusteriAccessFilter(_currentUserService, x => x.EkleyenFirmaId, x => x.EkleyenSubeId)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.MusteriId, cancellationToken);

        if (musteri == null)
            return new List<MusteriHavuzPoliceDto>();

        // 2. Havuz poliçelerini müşteri bilgileriyle eşleştir
        var poolQuery = _context.PoliceHavuzlari.AsQueryable();

        // Firma filtresi
        if (_currentUserService.FirmaId.HasValue)
            poolQuery = poolQuery.Where(x => x.IsOrtagiFirmaId == _currentUserService.FirmaId.Value);

        // Müşteri eşleştirme: MusteriId OR TcKimlikNo OR VergiNo
        var tc = musteri.TcKimlikNo;
        var vkn = musteri.VergiNo;
        var musteriId = musteri.Id;

        poolQuery = poolQuery.Where(p =>
            p.MusteriId == musteriId ||
            (!string.IsNullOrEmpty(tc) && p.TcKimlikNo == tc) ||
            (!string.IsNullOrEmpty(vkn) && p.VergiNo == vkn));

        var havuzPoliceler = await poolQuery
            .OrderByDescending(p => p.TanzimTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (havuzPoliceler.Count == 0)
            return new List<MusteriHavuzPoliceDto>();

        // 3. Lookup dictionary'leri
        var bransIds = havuzPoliceler.Select(p => p.BransId).Distinct().ToList();
        var sirketIds = havuzPoliceler.Select(p => p.SigortaSirketiId).Distinct().ToList();

        var bransDict = await _context.PoliceTurleri
            .Where(b => bransIds.Contains(b.Id))
            .AsNoTracking()
            .ToDictionaryAsync(b => b.Id, b => b.Turu ?? $"Branş #{b.Id}", cancellationToken);

        var sirketDict = await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Ad, cancellationToken);

        // 4. DTO'ya dönüştür
        return havuzPoliceler.Select(p =>
        {
            bransDict.TryGetValue(p.BransId, out var bransAdi);
            sirketDict.TryGetValue(p.SigortaSirketiId, out var sirketAdi);

            return new MusteriHavuzPoliceDto
            {
                Id = p.Id,
                PoliceNo = p.PoliceNo,
                BransAdi = bransAdi ?? $"Branş #{p.BransId}",
                SirketAdi = sirketAdi ?? $"Şirket #{p.SigortaSirketiId}",
                BrutPrim = p.BrutPrim,
                BaslangicTarihi = p.BaslangicTarihi,
                BitisTarihi = p.BitisTarihi,
                TanzimTarihi = p.TanzimTarihi,
                Plaka = p.Plaka,
                PoliceTipi = p.PoliceTipi
            };
        }).ToList();
    }
}
