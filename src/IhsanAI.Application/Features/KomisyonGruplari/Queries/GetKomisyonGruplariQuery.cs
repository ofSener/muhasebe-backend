using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.KomisyonGruplari.Dtos;

namespace IhsanAI.Application.Features.KomisyonGruplari.Queries;

/// <summary>
/// Tüm komisyon gruplarını listeler
/// </summary>
public record GetKomisyonGruplariQuery(int FirmaId) : IRequest<List<KomisyonGrubuDto>>;

public class GetKomisyonGruplariQueryHandler : IRequestHandler<GetKomisyonGruplariQuery, List<KomisyonGrubuDto>>
{
    private readonly IApplicationDbContext _context;

    public GetKomisyonGruplariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<KomisyonGrubuDto>> Handle(GetKomisyonGruplariQuery request, CancellationToken cancellationToken)
    {
        return await _context.KomisyonGruplari
            .Where(g => g.FirmaId == request.FirmaId)
            .Select(g => new KomisyonGrubuDto
            {
                Id = g.Id,
                GrupAdi = g.GrupAdi,
                Aciklama = g.Aciklama,
                Aktif = g.Aktif,
                KuralSayisi = g.Kurallar.Count,
                UyeSayisi = g.Uyeler.Count,
                SubeSayisi = g.Subeler.Count,
                EklenmeTarihi = g.EklenmeTarihi,
                GuncellenmeTarihi = g.GuncellenmeTarihi
            })
            .OrderBy(g => g.GrupAdi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

/// <summary>
/// Komisyon grubu detayını getirir (kurallar ve üyelerle birlikte)
/// </summary>
public record GetKomisyonGrubuDetayQuery(int Id, int FirmaId) : IRequest<KomisyonGrubuDetayDto?>;

public class GetKomisyonGrubuDetayQueryHandler : IRequestHandler<GetKomisyonGrubuDetayQuery, KomisyonGrubuDetayDto?>
{
    private readonly IApplicationDbContext _context;

    public GetKomisyonGrubuDetayQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<KomisyonGrubuDetayDto?> Handle(GetKomisyonGrubuDetayQuery request, CancellationToken cancellationToken)
    {
        var grup = await _context.KomisyonGruplari
            .Include(g => g.Kurallar)
            .Include(g => g.Uyeler)
            .Include(g => g.Subeler)
            .Where(g => g.Id == request.Id && g.FirmaId == request.FirmaId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (grup == null)
            return null;

        // Sigorta şirketleri ve branşları yükle
        var sigortaSirketleri = await _context.SigortaSirketleri
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.SirketAdi ?? "", cancellationToken);

        var branslar = await _context.PoliceTurleri
            .AsNoTracking()
            .ToDictionaryAsync(b => b.Id, b => b.Turu ?? "", cancellationToken);

        // Üye adlarını yükle
        var uyeIds = grup.Uyeler.Select(u => u.UyeId).ToList();
        var kullanicilar = await _context.Kullanicilar
            .Where(k => uyeIds.Contains(k.Id))
            .AsNoTracking()
            .ToDictionaryAsync(k => k.Id, k => $"{k.Adi ?? ""} {k.Soyadi ?? ""}".Trim(), cancellationToken);

        // Şube adlarını yükle
        var subeIds = grup.Subeler.Select(s => s.SubeId).ToList();
        var subeler = await _context.Subeler
            .Where(s => subeIds.Contains(s.Id))
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.SubeAdi ?? "", cancellationToken);

        return new KomisyonGrubuDetayDto
        {
            Id = grup.Id,
            GrupAdi = grup.GrupAdi,
            Aciklama = grup.Aciklama,
            Aktif = grup.Aktif,
            EklenmeTarihi = grup.EklenmeTarihi,
            GuncellenmeTarihi = grup.GuncellenmeTarihi,
            Kurallar = grup.Kurallar
                .OrderByDescending(k => k.OncelikPuani)
                .ThenByDescending(k => k.EsikDeger) // Büyük eşik önce kontrol edilir
                .Select(k => new KomisyonKuraliDto
                {
                    Id = k.Id,
                    SigortaSirketiId = k.SigortaSirketiId,
                    SigortaSirketiAdi = k.SigortaSirketiId == 9999
                        ? "Varsayılan (Tümü)"
                        : (sigortaSirketleri.TryGetValue(k.SigortaSirketiId, out var sirketAdi) ? sirketAdi : $"#{k.SigortaSirketiId}"),
                    BransId = k.BransId,
                    BransAdi = k.BransId == 9999
                        ? "Varsayılan (Tümü)"
                        : (branslar.TryGetValue(k.BransId, out var bransAdi) ? bransAdi : $"#{k.BransId}"),
                    KosulAlani = k.KosulAlani,
                    Operator = k.Operator,
                    EsikDeger = k.EsikDeger,
                    KomisyonOrani = k.KomisyonOrani,
                    OncelikPuani = k.OncelikPuani
                })
                .ToList(),
            Uyeler = grup.Uyeler
                .Select(u => new KomisyonGrubuUyesiDto
                {
                    Id = u.Id,
                    UyeId = u.UyeId,
                    UyeAdi = kullanicilar.TryGetValue(u.UyeId, out var ad) && !string.IsNullOrWhiteSpace(ad) ? ad : "İsimsiz",
                    EklenmeTarihi = u.EklenmeTarihi
                })
                .ToList(),
            Subeler = grup.Subeler
                .Select(s => new KomisyonGrubuSubesiDto
                {
                    Id = s.Id,
                    SubeId = s.SubeId,
                    SubeAdi = subeler.TryGetValue(s.SubeId, out var subeAdi) && !string.IsNullOrWhiteSpace(subeAdi) ? subeAdi : "İsimsiz",
                    EklenmeTarihi = s.EklenmeTarihi
                })
                .ToList()
        };
    }
}
