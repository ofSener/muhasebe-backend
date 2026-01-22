using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Policeler.Commands;

public record BatchSendPoliciesToPoolCommand(List<int> YakalananPoliceIds) : IRequest<BatchSendToPoolResultDto>;

public record BatchSendToPoolResultDto
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<int> FailedIds { get; init; } = new List<int>();
    public List<int> CreatedPoolIds { get; init; } = new List<int>();
}

public class BatchSendPoliciesToPoolCommandHandler : IRequestHandler<BatchSendPoliciesToPoolCommand, BatchSendToPoolResultDto>
{
    private readonly IApplicationDbContext _context;

    public BatchSendPoliciesToPoolCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BatchSendToPoolResultDto> Handle(BatchSendPoliciesToPoolCommand request, CancellationToken cancellationToken)
    {
        var yakalananlar = await _context.YakalananPoliceler
            .Where(y => request.YakalananPoliceIds.Contains(y.Id))
            .ToListAsync(cancellationToken);

        var successCount = 0;
        var failedIds = new List<int>();
        var createdPoolIds = new List<int>();

        foreach (var id in request.YakalananPoliceIds)
        {
            var yakalanan = yakalananlar.FirstOrDefault(y => y.Id == id);
            if (yakalanan == null)
            {
                failedIds.Add(id);
                continue;
            }

            var policeHavuz = new PoliceHavuz
            {
                PoliceTipi = yakalanan.PoliceTuru.ToString(),
                PoliceNo = yakalanan.PoliceNumarasi,
                Plaka = yakalanan.Plaka,
                ZeyilNo = 0,
                SigortaSirketiId = yakalanan.SigortaSirketi,
                TanzimTarihi = yakalanan.TanzimTarihi,
                BaslangicTarihi = yakalanan.BaslangicTarihi,
                BitisTarihi = yakalanan.BitisTarihi,
                SigortaEttirenId = yakalanan.MusteriId ?? 0,
                BrutPrim = (decimal)yakalanan.BrutPrim,
                NetPrim = (decimal)yakalanan.NetPrim,
                Vergi = 0,
                Komisyon = 0,
                BransId = yakalanan.PoliceTuru,
                DisPolice = yakalanan.DisPolice ?? 0,
                MusteriId = yakalanan.MusteriId ?? 0,
                PoliceTespitKaynakId = 0,
                IsOrtagiFirmaId = yakalanan.FirmaId,
                IsOrtagiSubeId = yakalanan.ProduktorSubeId,
                IsOrtagiUyeId = yakalanan.ProduktorId,
                IsOrtagiKomisyonOrani = 0,
                IsOrtagiKomisyon = 0,
                IsOrtagiEslestirmeKriteri = 0,
                KaynakDosyaId = 0,
                KayitDurumu = 1,
                EklenmeTarihi = DateTime.UtcNow,
                GuncelleyenKullaniciId = yakalanan.UyeId,
                Aciklama = yakalanan.Aciklama
            };

            _context.PoliceHavuzlari.Add(policeHavuz);
            successCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new BatchSendToPoolResultDto
        {
            SuccessCount = successCount,
            FailedCount = failedIds.Count,
            FailedIds = failedIds,
            CreatedPoolIds = createdPoolIds
        };
    }
}
