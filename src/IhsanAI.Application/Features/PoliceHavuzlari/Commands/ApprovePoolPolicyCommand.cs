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

        // Fetch matching captured policy to get ProduktorId and ProduktorSubeId
        var yakalananPolice = await _context.YakalananPoliceler
            .FirstOrDefaultAsync(x =>
                x.PoliceNumarasi == poolPolicy.PoliceNo &&
                x.SigortaSirketi == poolPolicy.SigortaSirketiId &&
                x.IsDeleted != 1,  // Exclude deleted records
                cancellationToken);

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
                x.PoliceNumarasi == poolPolicy.PoliceNo &&
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
            SigortaSirketiId = poolPolicy.SigortaSirketiId,
            PoliceTuruId = poolPolicy.BransId,
            PoliceNumarasi = poolPolicy.PoliceNo,
            Plaka = yakalananPolice?.Plaka ?? poolPolicy.Plaka,
            TanzimTarihi = poolPolicy.TanzimTarihi,
            BaslangicTarihi = poolPolicy.BaslangicTarihi,
            BitisTarihi = poolPolicy.BitisTarihi,
            BrutPrim = (float)poolPolicy.BrutPrim,
            NetPrim = (float)poolPolicy.NetPrim,
            SigortaliAdi = null, // Müşteri bilgisi ayrı tabloda

            // Yakalanan poliçedeki bilgiler (öncelikle yakalanan poliçeden, yoksa IsOrtagi kolonlarından)
            ProduktorId = yakalananPolice?.ProduktorId ?? poolPolicy.IsOrtagiUyeId,
            ProduktorSubeId = yakalananPolice?.ProduktorSubeId ?? poolPolicy.IsOrtagiSubeId,
            UyeId = yakalananPolice?.UyeId ?? poolPolicy.IsOrtagiUyeId,
            SubeId = yakalananPolice?.SubeId ?? poolPolicy.IsOrtagiSubeId,
            FirmaId = yakalananPolice?.FirmaId ?? poolPolicy.IsOrtagiFirmaId,

            MusteriId = poolPolicy.MusteriId,
            CepTelefonu = null,
            GuncelleyenUyeId = _currentUserService.UyeId,
            DisPolice = poolPolicy.DisPolice,
            AcenteAdi = poolPolicy.PoliceKesenPersonel, // PoliceKesenPersonel → AcenteAdi
            AcenteNo = string.Empty,
            EklenmeTarihi = DateTime.UtcNow,
            Aciklama = poolPolicy.Aciklama,
            Komisyon = (float?)poolPolicy.Komisyon,
            Zeyil = 0,
            ZeyilNo = poolPolicy.ZeyilNo,
            YenilemeDurumu = string.IsNullOrEmpty(poolPolicy.YenilemeDurumu) ? 0 : (poolPolicy.YenilemeDurumu == "Yenilenmiş" ? 1 : 0),
            OnayDurumu = 1  // Onaylandı
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
