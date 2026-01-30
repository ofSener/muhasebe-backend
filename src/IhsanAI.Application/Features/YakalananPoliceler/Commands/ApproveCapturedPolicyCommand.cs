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
                p.SigortaSirketiId == capturedPolicy.SigortaSirketi &&
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
            SigortaSirketiId = capturedPolicy.SigortaSirketi,
            PoliceTuruId = capturedPolicy.PoliceTuru,
            PoliceNumarasi = capturedPolicy.PoliceNumarasi,
            Plaka = capturedPolicy.Plaka ?? string.Empty,

            // Tarih bilgileri
            TanzimTarihi = capturedPolicy.TanzimTarihi,
            BaslangicTarihi = capturedPolicy.BaslangicTarihi,
            BitisTarihi = capturedPolicy.BitisTarihi,

            // Prim bilgileri
            BrutPrim = capturedPolicy.BrutPrim,
            NetPrim = capturedPolicy.NetPrim,
            Komisyon = 0, // YakalananPolice'de Komisyon alanı yok, varsayılan 0

            // Sigortalı bilgileri
            SigortaliAdi = capturedPolicy.SigortaliAdi,
            MusteriId = capturedPolicy.MusteriId, // YakalananPolice'de var
            CepTelefonu = capturedPolicy.CepTelefonu, // YakalananPolice'de var

            // Zeyil bilgileri
            Zeyil = 0, // sbyte: 0 = Zeyil Değil
            ZeyilNo = 0,

            // Durum bilgileri
            OnayDurumu = 1, // Onaylandı olarak kaydet
            YenilemeDurumu = 0, // Yenileme durumu bilinmiyor

            // Acente bilgileri
            AcenteAdi = capturedPolicy.AcenteAdi,
            AcenteNo = capturedPolicy.AcenteNo ?? string.Empty,

            // Dış poliçe
            DisPolice = capturedPolicy.DisPolice ?? 0,

            // Tarih bilgileri
            EklenmeTarihi = DateTime.Now,
            GuncellenmeTarihi = null,

            // Açıklama
            Aciklama = capturedPolicy.Aciklama,

            // Güncelleyen kullanıcı
            GuncelleyenUyeId = _currentUserService.UyeId
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
