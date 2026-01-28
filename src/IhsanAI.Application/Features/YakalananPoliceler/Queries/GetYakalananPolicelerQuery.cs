using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.DTOs;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.YakalananPoliceler.Queries;

public record GetYakalananPolicelerQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? SortBy = null,
    string? SortDir = null,
    int? Limit = 500
) : IRequest<List<YakalananPoliceDto>>;

public class GetYakalananPolicelerQueryHandler : IRequestHandler<GetYakalananPolicelerQuery, List<YakalananPoliceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetYakalananPolicelerQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<YakalananPoliceDto>> Handle(GetYakalananPolicelerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler
            .AsQueryable()
            .ApplyAuthorizationFilters(_currentUserService);

        // Tarih filtreleme (TanzimTarihi'ne göre)
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

        // Sıralama - varsayılan BaslangicTarihi DESC
        query = (request.SortBy?.ToLower(), request.SortDir?.ToLower()) switch
        {
            ("baslangictarihi", "asc") => query.OrderBy(x => x.BaslangicTarihi),
            ("baslangictarihi", "desc") => query.OrderByDescending(x => x.BaslangicTarihi),
            ("tanzimtarihi", "asc") => query.OrderBy(x => x.TanzimTarihi),
            ("tanzimtarihi", "desc") => query.OrderByDescending(x => x.TanzimTarihi),
            ("brutprim", "asc") => query.OrderBy(x => x.BrutPrim),
            ("brutprim", "desc") => query.OrderByDescending(x => x.BrutPrim),
            ("sigortaliadi", "asc") => query.OrderBy(x => x.SigortaliAdi),
            ("sigortaliadi", "desc") => query.OrderByDescending(x => x.SigortaliAdi),
            ("policenumara", "asc") => query.OrderBy(x => x.PoliceNumarasi),
            ("policenumara", "desc") => query.OrderByDescending(x => x.PoliceNumarasi),
            ("eklenmeTarihi", "asc") => query.OrderBy(x => x.EklenmeTarihi),
            ("eklenmeTarihi", "desc") => query.OrderByDescending(x => x.EklenmeTarihi),
            _ => query.OrderByDescending(x => x.TanzimTarihi) // Default: TanzimTarihi DESC
        };

        // Lookup tabloları - IdEski ile de eşleştir (eski sistemle uyumluluk)
        var sigortaSirketleriList = await _context.SigortaSirketleri
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Hem Id hem IdEski ile lookup dictionary oluştur
        var sigortaSirketleri = new Dictionary<int, string>();
        foreach (var s in sigortaSirketleriList)
        {
            if (!sigortaSirketleri.ContainsKey(s.Id))
                sigortaSirketleri[s.Id] = s.Ad;
            if (s.IdEski.HasValue && !sigortaSirketleri.ContainsKey(s.IdEski.Value))
                sigortaSirketleri[s.IdEski.Value] = s.Ad;
        }

        var policeTurleri = await _context.PoliceTurleri
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.Turu, cancellationToken);

        var subeler = await _context.Subeler
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.SubeAdi, cancellationToken);

        // Kullanıcılar - Ad Soyad için
        var kullanicilar = await _context.Kullanicilar
            .AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => $"{u.Adi} {u.Soyadi}".Trim(), cancellationToken);

        var items = await query
            .Take(request.Limit ?? 500)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items.Select(p => new YakalananPoliceDto
        {
            Id = p.Id,
            SigortaSirketiId = p.SigortaSirketi,
            SigortaSirketiAdi = sigortaSirketleri.TryGetValue(p.SigortaSirketi, out var sirket) ? sirket : null,
            PoliceTuruId = p.PoliceTuru,
            PoliceTuruAdi = policeTurleri.TryGetValue(p.PoliceTuru, out var tur) ? tur : null,
            PoliceNo = p.PoliceNumarasi,
            Plaka = p.Plaka,
            TanzimTarihi = p.TanzimTarihi,
            BaslangicTarihi = p.BaslangicTarihi,
            BitisTarihi = p.BitisTarihi,
            BrutPrim = (decimal)p.BrutPrim,
            NetPrim = (decimal)p.NetPrim,
            SigortaliAdi = p.SigortaliAdi,
            ProduktorId = p.ProduktorId,
            ProduktorSubeId = p.ProduktorSubeId,
            UyeId = p.UyeId,
            UyeAdi = kullanicilar.TryGetValue(p.UyeId, out var kullanici) ? kullanici : null,
            SubeId = p.SubeId,
            SubeAdi = subeler.TryGetValue(p.SubeId, out var sube) ? sube : null,
            FirmaId = p.FirmaId,
            MusteriId = p.MusteriId,
            CepTelefonu = p.CepTelefonu,
            GuncelleyenUyeId = p.GuncelleyenUyeId,
            DisPolice = p.DisPolice,
            AcenteAdi = p.AcenteAdi,
            AcenteNo = p.AcenteNo,
            EklenmeTarihi = p.EklenmeTarihi,
            GuncellenmeTarihi = p.GuncellenmeTarihi,
            Aciklama = p.Aciklama
        }).ToList();
    }
}

public record GetYakalananPoliceByIdQuery(int Id) : IRequest<YakalananPoliceDto?>;

public class GetYakalananPoliceByIdQueryHandler : IRequestHandler<GetYakalananPoliceByIdQuery, YakalananPoliceDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetYakalananPoliceByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<YakalananPoliceDto?> Handle(GetYakalananPoliceByIdQuery request, CancellationToken cancellationToken)
    {
        var policy = await _context.YakalananPoliceler
            .Where(x => x.Id == request.Id)
            .ApplyAuthorizationFilters(_currentUserService)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (policy == null) return null;

        // Lookup tabloları
        var sigortaSirketi = await _context.SigortaSirketleri
            .Where(s => s.Id == policy.SigortaSirketi)
            .Select(s => s.Ad)
            .FirstOrDefaultAsync(cancellationToken);

        var policeTuru = await _context.PoliceTurleri
            .Where(p => p.Id == policy.PoliceTuru)
            .Select(p => p.Turu)
            .FirstOrDefaultAsync(cancellationToken);

        var subeAdi = await _context.Subeler
            .Where(s => s.Id == policy.SubeId)
            .Select(s => s.SubeAdi)
            .FirstOrDefaultAsync(cancellationToken);

        var uyeAdi = await _context.Kullanicilar
            .Where(u => u.Id == policy.UyeId)
            .Select(u => $"{u.Adi} {u.Soyadi}".Trim())
            .FirstOrDefaultAsync(cancellationToken);

        return new YakalananPoliceDto
        {
            Id = policy.Id,
            SigortaSirketiId = policy.SigortaSirketi,
            SigortaSirketiAdi = sigortaSirketi,
            PoliceTuruId = policy.PoliceTuru,
            PoliceTuruAdi = policeTuru,
            PoliceNo = policy.PoliceNumarasi,
            Plaka = policy.Plaka,
            TanzimTarihi = policy.TanzimTarihi,
            BaslangicTarihi = policy.BaslangicTarihi,
            BitisTarihi = policy.BitisTarihi,
            BrutPrim = (decimal)policy.BrutPrim,
            NetPrim = (decimal)policy.NetPrim,
            SigortaliAdi = policy.SigortaliAdi,
            ProduktorId = policy.ProduktorId,
            ProduktorSubeId = policy.ProduktorSubeId,
            UyeId = policy.UyeId,
            UyeAdi = uyeAdi,
            SubeId = policy.SubeId,
            SubeAdi = subeAdi,
            FirmaId = policy.FirmaId,
            MusteriId = policy.MusteriId,
            CepTelefonu = policy.CepTelefonu,
            GuncelleyenUyeId = policy.GuncelleyenUyeId,
            DisPolice = policy.DisPolice,
            AcenteAdi = policy.AcenteAdi,
            AcenteNo = policy.AcenteNo,
            EklenmeTarihi = policy.EklenmeTarihi,
            GuncellenmeTarihi = policy.GuncellenmeTarihi,
            Aciklama = policy.Aciklama
        };
    }
}
