using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Extensions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Musteriler.Queries;

// DTO for customer list with policy stats
public record MusteriListDto
{
    public int Id { get; init; }
    public sbyte? SahipTuru { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? Gsm { get; init; }
    public string? Email { get; init; }
    public DateTime? EklenmeZamani { get; init; }
    public int PoliceSayisi { get; init; }
    public decimal ToplamPrim { get; init; }
}

public record GetMusterilerQuery(int? EkleyenFirmaId = null, int? Limit = 100) : IRequest<List<MusteriListDto>>;

public class GetMusterilerQueryHandler : IRequestHandler<GetMusterilerQuery, List<MusteriListDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMusterilerQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<MusteriListDto>> Handle(GetMusterilerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Musteriler.AsQueryable();

        // GÜVENLİK: Token'dan gelen FirmaId ile filtrele, client'a güvenme!
        query = query.ApplyFirmaFilterNullable(_currentUserService, x => x.EkleyenFirmaId);

        // LEFT JOIN with Police table to get policy count and total premium
        var result = await query
            .GroupJoin(
                _context.Policeler.Where(p => p.OnayDurumu == 1),
                m => m.Id,
                p => p.MusteriId,
                (musteri, policeler) => new { musteri, policeler })
            .SelectMany(
                x => x.policeler.DefaultIfEmpty(),
                (x, police) => new { x.musteri, police })
            .GroupBy(x => new
            {
                x.musteri.Id,
                x.musteri.SahipTuru,
                x.musteri.TcKimlikNo,
                x.musteri.VergiNo,
                x.musteri.Adi,
                x.musteri.Soyadi,
                x.musteri.Gsm,
                x.musteri.Email,
                x.musteri.EklenmeZamani
            })
            .Select(g => new MusteriListDto
            {
                Id = g.Key.Id,
                SahipTuru = g.Key.SahipTuru,
                TcKimlikNo = g.Key.TcKimlikNo,
                VergiNo = g.Key.VergiNo,
                Adi = g.Key.Adi,
                Soyadi = g.Key.Soyadi,
                Gsm = g.Key.Gsm,
                Email = g.Key.Email,
                EklenmeZamani = g.Key.EklenmeZamani,
                PoliceSayisi = g.Count(x => x.police != null),
                ToplamPrim = g.Sum(x => x.police != null ? x.police.BrutPrim : 0)
            })
            .OrderByDescending(x => x.EklenmeZamani)
            .Take(request.Limit ?? 100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return result;
    }
}

// DTO for customer detail with full info and policy stats
public record MusteriDetailDto
{
    public int Id { get; init; }
    public sbyte? SahipTuru { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public string? TcVergiNo { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? DogumYeri { get; init; }
    public DateTime? DogumTarihi { get; init; }
    public string? Cinsiyet { get; init; }
    public string? BabaAdi { get; init; }
    public string? Gsm { get; init; }
    public string? Gsm2 { get; init; }
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? Meslek { get; init; }
    public string? YasadigiIl { get; init; }
    public string? YasadigiIlce { get; init; }
    public int? Boy { get; init; }
    public int? Kilo { get; init; }
    public int? EkleyenFirmaId { get; init; }
    public int? EkleyenUyeId { get; init; }
    public int? EkleyenSubeId { get; init; }
    public DateTime? EklenmeZamani { get; init; }
    public DateTime? GuncellenmeZamani { get; init; }
    public int? GuncelleyenUyeId { get; init; }
    public int PoliceSayisi { get; init; }
    public decimal ToplamPrim { get; init; }
}

public record GetMusteriByIdQuery(int Id) : IRequest<MusteriDetailDto?>;

public class GetMusteriByIdQueryHandler : IRequestHandler<GetMusteriByIdQuery, MusteriDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMusteriByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MusteriDetailDto?> Handle(GetMusteriByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Musteriler.AsQueryable();

        // GÜVENLİK: Token'dan gelen FirmaId ile filtrele
        query = query.ApplyFirmaFilterNullable(_currentUserService, x => x.EkleyenFirmaId);

        var musteri = await query
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (musteri == null)
            return null;

        // Get policy stats
        var policeSayisi = await _context.Policeler
            .Where(p => p.MusteriId == musteri.Id && p.OnayDurumu == 1)
            .CountAsync(cancellationToken);

        var toplamPrim = await _context.Policeler
            .Where(p => p.MusteriId == musteri.Id && p.OnayDurumu == 1)
            .SumAsync(p => p.BrutPrim, cancellationToken);

        return new MusteriDetailDto
        {
            Id = musteri.Id,
            SahipTuru = musteri.SahipTuru,
            TcKimlikNo = musteri.TcKimlikNo,
            VergiNo = musteri.VergiNo,
            TcVergiNo = musteri.TcVergiNo,
            Adi = musteri.Adi,
            Soyadi = musteri.Soyadi,
            DogumYeri = musteri.DogumYeri,
            DogumTarihi = musteri.DogumTarihi,
            Cinsiyet = musteri.Cinsiyet,
            BabaAdi = musteri.BabaAdi,
            Gsm = musteri.Gsm,
            Gsm2 = musteri.Gsm2,
            Telefon = musteri.Telefon,
            Email = musteri.Email,
            Meslek = musteri.Meslek,
            YasadigiIl = musteri.YasadigiIl,
            YasadigiIlce = musteri.YasadigiIlce,
            Boy = musteri.Boy,
            Kilo = musteri.Kilo,
            EkleyenFirmaId = musteri.EkleyenFirmaId,
            EkleyenUyeId = musteri.EkleyenUyeId,
            EkleyenSubeId = musteri.EkleyenSubeId,
            EklenmeZamani = musteri.EklenmeZamani,
            GuncellenmeZamani = musteri.GuncellenmeZamani,
            GuncelleyenUyeId = musteri.GuncelleyenUyeId,
            PoliceSayisi = policeSayisi,
            ToplamPrim = toplamPrim
        };
    }
}

public record MusteriSearchDto
{
    public int Id { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? Gsm { get; init; }
    public string? Email { get; init; }
}

public record SearchCustomersQuery(string Name, int? EkleyenFirmaId = null, int Limit = 20) : IRequest<List<MusteriSearchDto>>;

public class SearchCustomersQueryHandler : IRequestHandler<SearchCustomersQuery, List<MusteriSearchDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SearchCustomersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<MusteriSearchDto>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Musteriler.AsQueryable();

        // GÜVENLİK: Token'dan gelen FirmaId ile filtrele, client'a güvenme!
        query = query.ApplyFirmaFilterNullable(_currentUserService, x => x.EkleyenFirmaId);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var searchTerm = request.Name.ToLower();
            query = query.Where(x =>
                (x.Adi != null && x.Adi.ToLower().Contains(searchTerm)) ||
                (x.Soyadi != null && x.Soyadi.ToLower().Contains(searchTerm)) ||
                (x.TcKimlikNo != null && x.TcKimlikNo.Contains(searchTerm)));
        }

        return await query
            .OrderBy(x => x.Adi)
            .ThenBy(x => x.Soyadi)
            .Take(request.Limit)
            .Select(x => new MusteriSearchDto
            {
                Id = x.Id,
                Adi = x.Adi,
                Soyadi = x.Soyadi,
                TcKimlikNo = x.TcKimlikNo,
                Gsm = x.Gsm,
                Email = x.Email
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
