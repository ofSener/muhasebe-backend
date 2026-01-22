using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.AcenteKodlari.Queries;

// DTO for agency code with insurance company name
public record AcenteKoduDto
{
    public int Id { get; init; }
    public int SigortaSirketiId { get; init; }
    public string SigortaSirketiAdi { get; init; } = string.Empty;
    public string AcenteKoduDeger { get; init; } = string.Empty;
    public string AcenteAdi { get; init; } = string.Empty;
    public sbyte DisAcente { get; init; }
    public int FirmaId { get; init; }
    public DateTime? EklenmeTarihi { get; init; }
    public DateTime? GuncellenmeTarihi { get; init; }
}

public record GetAcenteKodlariQuery(int? FirmaId = null) : IRequest<List<AcenteKoduDto>>;

public class GetAcenteKodlariQueryHandler : IRequestHandler<GetAcenteKodlariQuery, List<AcenteKoduDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAcenteKodlariQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AcenteKoduDto>> Handle(GetAcenteKodlariQuery request, CancellationToken cancellationToken)
    {
        var query = from ak in _context.AcenteKodlari
                    join ss in _context.SigortaSirketleri on ak.SigortaSirketiId equals ss.Id into ssJoin
                    from ss in ssJoin.DefaultIfEmpty()
                    select new AcenteKoduDto
                    {
                        Id = ak.Id,
                        SigortaSirketiId = ak.SigortaSirketiId,
                        SigortaSirketiAdi = ss != null ? ss.Ad : "Bilinmiyor",
                        AcenteKoduDeger = ak.AcenteKoduDeger,
                        AcenteAdi = ak.AcenteAdi,
                        DisAcente = ak.DisAcente,
                        FirmaId = ak.FirmaId,
                        EklenmeTarihi = ak.EklenmeTarihi,
                        GuncellenmeTarihi = ak.GuncellenmeTarihi
                    };

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        return await query
            .OrderBy(x => x.AcenteAdi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetAcenteKoduByIdQuery(int Id) : IRequest<AcenteKoduDto?>;

public class GetAcenteKoduByIdQueryHandler : IRequestHandler<GetAcenteKoduByIdQuery, AcenteKoduDto?>
{
    private readonly IApplicationDbContext _context;

    public GetAcenteKoduByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AcenteKoduDto?> Handle(GetAcenteKoduByIdQuery request, CancellationToken cancellationToken)
    {
        return await (from ak in _context.AcenteKodlari
                      join ss in _context.SigortaSirketleri on ak.SigortaSirketiId equals ss.Id into ssJoin
                      from ss in ssJoin.DefaultIfEmpty()
                      where ak.Id == request.Id
                      select new AcenteKoduDto
                      {
                          Id = ak.Id,
                          SigortaSirketiId = ak.SigortaSirketiId,
                          SigortaSirketiAdi = ss != null ? ss.Ad : "Bilinmiyor",
                          AcenteKoduDeger = ak.AcenteKoduDeger,
                          AcenteAdi = ak.AcenteAdi,
                          DisAcente = ak.DisAcente,
                          FirmaId = ak.FirmaId,
                          EklenmeTarihi = ak.EklenmeTarihi,
                          GuncellenmeTarihi = ak.GuncellenmeTarihi
                      })
                      .AsNoTracking()
                      .FirstOrDefaultAsync(cancellationToken);
    }
}
