using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.YakalananPoliceler.Commands;

/// <summary>
/// Yakalanan poliçeyi direkt olarak muhasebe_policeler_v2 tablosuna kaydeder (havuzu bypass eder)
/// </summary>
public record ApproveCapturedPolicyCommand(int CapturedPolicyId) : IRequest<ApproveCapturedPolicyResult>;

public record ApproveCapturedPolicyResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int? PolicyId { get; init; }
}

public class ApproveCapturedPolicyCommandHandler : IRequestHandler<ApproveCapturedPolicyCommand, ApproveCapturedPolicyResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ApproveCapturedPolicyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ApproveCapturedPolicyResult> Handle(ApproveCapturedPolicyCommand request, CancellationToken cancellationToken)
    {
        // Yakalanan poliçeyi bul
        var capturedPolicy = await _context.YakalananPoliceler
            .FirstOrDefaultAsync(x => x.Id == request.CapturedPolicyId, cancellationToken);

        if (capturedPolicy == null)
        {
            return new ApproveCapturedPolicyResult
            {
                Success = false,
                Message = "Yakalanan poliçe bulunamadı"
            };
        }

        // Aynı poliçe numarası ile zaten kayıtlı bir poliçe var mı kontrol et
        var existingPolicy = await _context.Policeler
            .AnyAsync(p =>
                p.PoliceNumarasi == capturedPolicy.PoliceNumarasi &&
                p.SigortaSirketi == capturedPolicy.SigortaSirketi &&
                p.ZeyilNo == 0, // Yakalanan poliçelerde zeyil no yok, 0 kabul ediyoruz
                cancellationToken);

        if (existingPolicy)
        {
            return new ApproveCapturedPolicyResult
            {
                Success = false,
                Message = "Bu poliçe numarası zaten kayıtlı"
            };
        }

        // Yeni poliçe kaydı oluştur
        var newPolicy = new Police
        {
            // Firma bilgileri - YakalananPolice'den gelecek
            FirmaId = capturedPolicy.FirmaId,
            SubeId = capturedPolicy.SubeId,
            ProduktorId = capturedPolicy.ProduktorId,
            ProduktorSubeId = capturedPolicy.ProduktorSubeId,
            UyeId = capturedPolicy.UyeId,

            // Poliçe bilgileri
            SigortaSirketi = capturedPolicy.SigortaSirketi,
            PoliceTuru = capturedPolicy.PoliceTuru,
            PoliceNumarasi = capturedPolicy.PoliceNumarasi,
            Plaka = capturedPolicy.Plaka,

            // Tarih bilgileri
            TanzimTarihi = capturedPolicy.TanzimTarihi,
            BaslangicTarihi = capturedPolicy.BaslangicTarihi,
            BitisTarihi = capturedPolicy.BitisTarihi,

            // Prim bilgileri
            BrutPrim = (float)capturedPolicy.BrutPrim,
            NetPrim = (float?)capturedPolicy.NetPrim,
            Komisyon = (float?)capturedPolicy.Komisyon,

            // Sigortalı bilgileri
            SigortaliAdi = capturedPolicy.SigortaliAdi,
            MusteriId = null, // YakalananPolice'de MusteriId yok
            CepTelefonu = null, // YakalananPolice'de telefon yok

            // Zeyil bilgileri
            Zeyil = false,
            ZeyilNo = 0,

            // Durum bilgileri
            OnayDurumu = 1, // Onaylandı olarak kaydet
            YenilemeDurumu = 0, // Yenileme durumu bilinmiyor

            // Acente bilgileri
            AcenteAdi = null, // YakalananPolice'de acente bilgisi yok
            AcenteNo = null,

            // Dış poliçe
            DisPolice = false,

            // Güncelleyen kullanıcı
            GuncelleyenUyeId = _currentUserService.UserId ?? 0
        };

        _context.Policeler.Add(newPolicy);
        await _context.SaveChangesAsync(cancellationToken);

        // Başarılı kayıt sonrası yakalanan poliçeyi silebiliriz (opsiyonel)
        // _context.YakalananPoliceler.Remove(capturedPolicy);
        // await _context.SaveChangesAsync(cancellationToken);

        return new ApproveCapturedPolicyResult
        {
            Success = true,
            Message = "Poliçe başarıyla kaydedildi",
            PolicyId = newPolicy.Id
        };
    }
}
