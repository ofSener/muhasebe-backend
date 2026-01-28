using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.PoliceHavuzlari.Commands;

/// <summary>
/// Havuzdaki poliçeyi onaylayıp ana poliçe tablosuna kaydeder
/// </summary>
public record ApprovePoolPolicyCommand(int PoolPolicyId) : IRequest<ApprovePoolPolicyResult>;

public record ApprovePoolPolicyResult
{
    public bool Success { get; init; }
    public int? NewPolicyId { get; init; }
    public string? ErrorMessage { get; init; }
}

public class ApprovePoolPolicyCommandHandler : IRequestHandler<ApprovePoolPolicyCommand, ApprovePoolPolicyResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ApprovePoolPolicyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ApprovePoolPolicyResult> Handle(ApprovePoolPolicyCommand request, CancellationToken cancellationToken)
    {
        // Havuzdaki poliçeyi bul
        var poolPolicy = await _context.PoliceHavuzlari
            .FirstOrDefaultAsync(x => x.Id == request.PoolPolicyId, cancellationToken);

        if (poolPolicy == null)
        {
            return new ApprovePoolPolicyResult
            {
                Success = false,
                ErrorMessage = "Havuzda bu poliçe bulunamadı"
            };
        }

        // Firma yetkisi kontrolü
        if (_currentUserService.FirmaId.HasValue && poolPolicy.IsOrtagiFirmaId != _currentUserService.FirmaId.Value)
        {
            return new ApprovePoolPolicyResult
            {
                Success = false,
                ErrorMessage = "Bu poliçeyi onaylama yetkiniz yok"
            };
        }

        // Aynı poliçe zaten var mı kontrol et
        var existingPolicy = await _context.Policeler
            .FirstOrDefaultAsync(x =>
                x.PoliceNo == poolPolicy.PoliceNo &&
                x.SigortaSirketiId == poolPolicy.SigortaSirketiId &&
                x.ZeyilNo == poolPolicy.ZeyilNo,
                cancellationToken);

        if (existingPolicy != null)
        {
            return new ApprovePoolPolicyResult
            {
                Success = false,
                ErrorMessage = $"Bu poliçe zaten kayıtlı (ID: {existingPolicy.Id})"
            };
        }

        // Yeni poliçe oluştur
        var newPolicy = new Police
        {
            PoliceTipi = poolPolicy.PoliceTipi,
            PoliceNo = poolPolicy.PoliceNo,
            Plaka = poolPolicy.Plaka,
            ZeyilNo = poolPolicy.ZeyilNo,
            YenilemeNo = poolPolicy.YenilemeNo,
            SigortaSirketiId = poolPolicy.SigortaSirketiId,
            TanzimTarihi = poolPolicy.TanzimTarihi,
            BaslangicTarihi = poolPolicy.BaslangicTarihi,
            BitisTarihi = poolPolicy.BitisTarihi,
            SigortaEttirenId = poolPolicy.SigortaEttirenId,
            BrutPrim = poolPolicy.BrutPrim,
            NetPrim = poolPolicy.NetPrim,
            Vergi = poolPolicy.Vergi,
            Komisyon = poolPolicy.Komisyon,
            BransId = poolPolicy.BransId,
            DisPolice = poolPolicy.DisPolice,
            MusteriId = poolPolicy.MusteriId,
            PoliceTespitKaynakId = poolPolicy.PoliceTespitKaynakId,
            IsOrtagiFirmaId = poolPolicy.IsOrtagiFirmaId,
            IsOrtagiSubeId = poolPolicy.IsOrtagiSubeId,
            IsOrtagiUyeId = poolPolicy.IsOrtagiUyeId,
            IsOrtagiKomisyonOrani = poolPolicy.IsOrtagiKomisyonOrani,
            IsOrtagiKomisyon = poolPolicy.IsOrtagiKomisyon,
            IsOrtagiEslestirmeKriteri = poolPolicy.IsOrtagiEslestirmeKriteri,
            IsOrtagiOnayDurumu = true,
            KaynakDosyaId = poolPolicy.KaynakDosyaId,
            KayitDurumu = 1, // Aktif
            OnayDurumu = 1,  // Onaylandı
            EklenmeTarihi = DateTime.UtcNow,
            GuncelleyenKullaniciId = _currentUserService.UyeId ?? 0,
            Kur = poolPolicy.Kur,
            Aciklama = poolPolicy.Aciklama,
            TahsilatAciklamasi = poolPolicy.TahsilatAciklamasi,
            PoliceKesenPersonel = poolPolicy.PoliceKesenPersonel,
            Sube = poolPolicy.Sube,
            YenilemeDurumu = poolPolicy.YenilemeDurumu,
            UretimTuru = poolPolicy.UretimTuru,
            KayitSekli = poolPolicy.KayitSekli,
            TaksitDurumu = poolPolicy.TaksitDurumu,
            TaksitSayisi = poolPolicy.TaksitSayisi,
            OdemeTipi = poolPolicy.OdemeTipi,
            MptsDurumu = poolPolicy.MptsDurumu,
            Mutabakat = poolPolicy.Mutabakat,
            NetKazanc = poolPolicy.NetKazanc,
            Iskonto = poolPolicy.Iskonto
        };

        _context.Policeler.Add(newPolicy);

        // Havuzdan sil
        _context.PoliceHavuzlari.Remove(poolPolicy);

        await _context.SaveChangesAsync(cancellationToken);

        return new ApprovePoolPolicyResult
        {
            Success = true,
            NewPolicyId = newPolicy.Id
        };
    }
}

/// <summary>
/// Birden fazla havuz poliçesini toplu onaylama
/// </summary>
public record BatchApprovePoolPoliciesCommand(List<int> PoolPolicyIds) : IRequest<BatchApprovePoolPoliciesResult>;

public record BatchApprovePoolPoliciesResult
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<string> Errors { get; init; } = new();
}

public class BatchApprovePoolPoliciesCommandHandler : IRequestHandler<BatchApprovePoolPoliciesCommand, BatchApprovePoolPoliciesResult>
{
    private readonly IMediator _mediator;

    public BatchApprovePoolPoliciesCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<BatchApprovePoolPoliciesResult> Handle(BatchApprovePoolPoliciesCommand request, CancellationToken cancellationToken)
    {
        int successCount = 0;
        int failedCount = 0;
        var errors = new List<string>();

        foreach (var poolPolicyId in request.PoolPolicyIds)
        {
            var result = await _mediator.Send(new ApprovePoolPolicyCommand(poolPolicyId), cancellationToken);

            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failedCount++;
                errors.Add($"ID {poolPolicyId}: {result.ErrorMessage}");
            }
        }

        return new BatchApprovePoolPoliciesResult
        {
            SuccessCount = successCount,
            FailedCount = failedCount,
            Errors = errors
        };
    }
}
