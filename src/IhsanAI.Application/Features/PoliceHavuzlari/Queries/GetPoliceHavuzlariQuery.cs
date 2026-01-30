using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.PoliceHavuzlari.Dtos;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.PoliceHavuzlari.Queries;

/// <summary>
/// Havuzdaki poliçeleri (muhasebe_policehavuz) yakalanan poliçelerle (muhasebe_yakalananpoliceler) karşılaştırarak listeler
/// </summary>
public record GetPoliceHavuzlariQuery : IRequest<PoliceHavuzListDto>
{
    public int? IsOrtagiFirmaId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Status { get; init; } // "all", "matched", "unmatched", "difference", "only-pool"
    public int? BransId { get; init; }
    public int? SigortaSirketiId { get; init; }
}

public class GetPoliceHavuzlariQueryHandler : IRequestHandler<GetPoliceHavuzlariQuery, PoliceHavuzListDto>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceHavuzlariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PoliceHavuzListDto> Handle(GetPoliceHavuzlariQuery request, CancellationToken cancellationToken)
    {
        // Havuzdaki poliçeleri çek
        var poolQuery = _context.PoliceHavuzlari.AsQueryable();

        if (request.IsOrtagiFirmaId.HasValue)
        {
            poolQuery = poolQuery.Where(x => x.IsOrtagiFirmaId == request.IsOrtagiFirmaId.Value);
        }

        if (request.BransId.HasValue)
        {
            poolQuery = poolQuery.Where(x => x.BransId == request.BransId.Value);
        }

        if (request.SigortaSirketiId.HasValue)
        {
            poolQuery = poolQuery.Where(x => x.SigortaSirketiId == request.SigortaSirketiId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            poolQuery = poolQuery.Where(x =>
                x.PoliceNo.ToLower().Contains(searchLower) ||
                (x.Plaka != null && x.Plaka.ToLower().Contains(searchLower)));
        }

        // Havuzdaki poliçeleri al
        var poolItems = await poolQuery
            .OrderByDescending(x => x.EklenmeTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Yakalanan poliçeleri çek (karşılaştırma için)
        var capturedQuery = _context.YakalananPoliceler.AsQueryable();

        if (request.IsOrtagiFirmaId.HasValue)
        {
            capturedQuery = capturedQuery.Where(x => x.FirmaId == request.IsOrtagiFirmaId.Value);
        }

        var capturedPolicies = await capturedQuery
            .Select(p => new
            {
                p.Id,
                PoliceNo = p.PoliceNumarasi,
                BrutPrim = (decimal)p.BrutPrim,
                p.SigortaSirketi,
                ZeyilNo = 0 // YakalananPoliceler tablosunda ZeyilNo yok
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Branş bilgilerini sigortapoliceturleri tablosundan çek
        var branslar = await _context.PoliceTurleri
            .AsNoTracking()
            .ToDictionaryAsync(pt => pt.Id, pt => pt.Turu ?? $"Tür #{pt.Id}", cancellationToken);

        // Sigorta şirketlerini çek
        var sigortaSirketleri = await _context.SigortaSirketleri
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => s.Ad, cancellationToken);

        // Her havuz kaydı için eşleşme durumu belirle
        var items = new List<PoliceHavuzItemDto>();
        int matchedCount = 0;
        int unmatchedCount = 0;
        int differenceCount = 0;
        int onlyPoolCount = 0;

        foreach (var poolItem in poolItems)
        {
            // PoliceNo ve SigortaSirketiId bazında eşleşme ara
            var match = capturedPolicies.FirstOrDefault(c =>
                c.PoliceNo == poolItem.PoliceNo &&
                c.SigortaSirketi == poolItem.SigortaSirketiId &&
                c.ZeyilNo == poolItem.ZeyilNo);

            string eslesmeDurumu;
            decimal? primFarki = null;

            if (match != null)
            {
                primFarki = poolItem.BrutPrim - match.BrutPrim;
                if (Math.Abs(primFarki.Value) < 0.01m) // Küçük farklılıkları göz ardı et
                {
                    eslesmeDurumu = "ESLESTI";
                    matchedCount++;
                }
                else
                {
                    eslesmeDurumu = "FARK_VAR";
                    differenceCount++;
                }
            }
            else
            {
                eslesmeDurumu = "AKTARIMDA"; // Sadece havuzda var
                onlyPoolCount++;
            }

            // Branş adını al
            string? bransAdi = null;
            if (branslar.TryGetValue(poolItem.BransId, out var brans))
            {
                bransAdi = brans;
            }

            // Sigorta şirketi adını al
            string? sirketAdi = null;
            if (sigortaSirketleri.TryGetValue(poolItem.SigortaSirketiId, out var sirket))
            {
                sirketAdi = sirket;
            }

            items.Add(new PoliceHavuzItemDto
            {
                Id = poolItem.Id,
                PoliceNo = poolItem.PoliceNo,
                SigortaliAdi = null, // Müşteri bilgisi ayrı tabloda
                Brans = bransAdi,
                BransId = poolItem.BransId,
                BrutPrim = poolItem.BrutPrim,
                BaslangicTarihi = poolItem.BaslangicTarihi,
                BitisTarihi = poolItem.BitisTarihi,
                EklenmeTarihi = poolItem.EklenmeTarihi,
                SigortaSirketi = sirketAdi,
                SigortaSirketiId = poolItem.SigortaSirketiId,
                Plaka = poolItem.Plaka,
                ZeyilNo = poolItem.ZeyilNo,
                PoliceTipi = poolItem.PoliceTipi,
                PoliceKesenPersonel = poolItem.PoliceKesenPersonel,
                EslesmeDurumu = eslesmeDurumu,
                YakalananPrim = match?.BrutPrim,
                PrimFarki = primFarki,
                YakalananPoliceId = match?.Id
            });
        }

        // Eşleşmeyenler sayısı = AKTARIMDA olanlar (sadece havuzda var)
        unmatchedCount = onlyPoolCount;

        // Status filtresi uygula
        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "all")
        {
            items = request.Status switch
            {
                "matched" => items.Where(x => x.EslesmeDurumu == "ESLESTI").ToList(),
                "unmatched" => items.Where(x => x.EslesmeDurumu == "AKTARIMDA").ToList(),
                "difference" => items.Where(x => x.EslesmeDurumu == "FARK_VAR").ToList(),
                "only-pool" => items.Where(x => x.EslesmeDurumu == "AKTARIMDA").ToList(),
                _ => items
            };
        }

        // Toplam sayı (filtreleme sonrası)
        var totalCount = items.Count;

        // Pagination uygula
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Toplam brüt prim (filtrelenmemiş tüm havuzdan)
        var totalBrutPrim = poolItems.Sum(x => x.BrutPrim);

        return new PoliceHavuzListDto
        {
            Items = pagedItems,
            TotalCount = poolItems.Count, // Toplam havuz sayısı (filtresiz)
            MatchedCount = matchedCount,
            UnmatchedCount = unmatchedCount,
            DifferenceCount = differenceCount,
            OnlyPoolCount = onlyPoolCount,
            TotalBrutPrim = totalBrutPrim,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }
}

public record GetPoliceHavuzByIdQuery(int Id) : IRequest<PoliceHavuz?>;

public class GetPoliceHavuzByIdQueryHandler : IRequestHandler<GetPoliceHavuzByIdQuery, PoliceHavuz?>
{
    private readonly IApplicationDbContext _context;

    public GetPoliceHavuzByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PoliceHavuz?> Handle(GetPoliceHavuzByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.PoliceHavuzlari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
