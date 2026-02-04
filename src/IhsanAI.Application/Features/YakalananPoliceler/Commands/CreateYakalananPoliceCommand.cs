using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.YakalananPoliceler.Commands;

/// <summary>
/// Yakalanan poliçe oluşturma komutu
/// </summary>
public record CreateYakalananPoliceCommand : IRequest<CreateYakalananPoliceResult>
{
    public int SigortaSirketi { get; init; }
    public int PoliceTuru { get; init; }
    public string PoliceNumarasi { get; init; } = string.Empty;
    public string Plaka { get; init; } = string.Empty;
    public DateTime TanzimTarihi { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public float BrutPrim { get; init; }
    public float NetPrim { get; init; }
    public string? SigortaliAdi { get; init; }
    public int? MusteriId { get; init; }
    public int? CepTelefonu { get; init; }
    public sbyte? DisPolice { get; init; }
    public string? AcenteAdi { get; init; }
    public string? AcenteNo { get; init; }
    public string? Aciklama { get; init; }
    public int? ProduktorId { get; init; }
    public int? UyeId { get; init; }
    public bool CanUpdate { get; init; } = true;
}

public record CreateYakalananPoliceResult
{
    public bool Success { get; init; }
    public int? Id { get; init; }
    public string? Error { get; init; }
    public bool IsUpdated { get; init; }  // true: güncellendi, false: yeni oluşturuldu
    public bool IsSkipped { get; init; }  // true: zaten vardı ve canUpdate=false olduğu için atlandı
}

public class CreateYakalananPoliceCommandHandler : IRequestHandler<CreateYakalananPoliceCommand, CreateYakalananPoliceResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IAcentelikService _acentelikService;

    public CreateYakalananPoliceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService,
        IAcentelikService acentelikService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
        _acentelikService = acentelikService;
    }

    public async Task<CreateYakalananPoliceResult> Handle(CreateYakalananPoliceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // JWT'den al
            var subeId = _currentUserService.SubeId ?? 0;
            var firmaId = _currentUserService.FirmaId ?? 0;

            // UyeId: request'ten gelirse onu kullan, yoksa JWT'den
            var uyeId = request.UyeId ?? _currentUserService.UyeId ?? 0;

            // ========== ACENTELIK KONTROLÜ ==========
            // PoliceYakalamaSecenekleri: "0" = Kontrol yok, "1" = Soft kontrol, "2" = Hard kontrol
            var policeYakalamaSecenekleri = _currentUserService.PoliceYakalamaSecenekleri ?? "0";
            sbyte disPolice = request.DisPolice ?? 0;

            if (policeYakalamaSecenekleri == "1" || policeYakalamaSecenekleri == "2")
            {
                // Acentelik kontrolü yap
                var acentelikVar = await _acentelikService.AcentelikVarMi(
                    firmaId,
                    request.SigortaSirketi,
                    request.AcenteNo,
                    request.AcenteAdi
                );

                if (!acentelikVar)
                {
                    if (policeYakalamaSecenekleri == "1")
                    {
                        // Soft kontrol: Acentelik yoksa DisPolice=1 yap ama kaydet
                        disPolice = 1;
                    }
                    else if (policeYakalamaSecenekleri == "2")
                    {
                        // Hard kontrol: Acentelik yoksa REDDET
                        return new CreateYakalananPoliceResult
                        {
                            Success = false,
                            Error = "Gelen poliçenin acenteliği, acenteliklerinizde bulunmadığından poliçe kaydedilmedi!"
                        };
                    }
                }
            }
            // ========================================

            // ProduktorSubeId'yi kullanıcılar tablosundan al
            int produktorSubeId = 0;
            if (request.ProduktorId.HasValue && request.ProduktorId.Value > 0)
            {
                var produktor = await _context.Kullanicilar
                    .Where(k => k.Id == request.ProduktorId.Value && k.FirmaId == firmaId)
                    .Select(k => k.SubeId)
                    .FirstOrDefaultAsync(cancellationToken);

                produktorSubeId = produktor ?? 0;
            }

            // Aynı poliçe var mı kontrol et (PoliceNo + SigortaSirketi + FirmaId)
            var existingPolice = await _context.YakalananPoliceler
                .Where(p => p.PoliceNumarasi == request.PoliceNumarasi
                         && p.SigortaSirketi == request.SigortaSirketi
                         && p.FirmaId == firmaId
                         && p.IsDeleted == 0)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingPolice != null)
            {
                // Poliçe zaten var
                if (!request.CanUpdate)
                {
                    // canUpdate=false ise güncelleme yapma, atla
                    return new CreateYakalananPoliceResult
                    {
                        Success = true,
                        Id = existingPolice.Id,
                        IsUpdated = false,
                        IsSkipped = true
                    };
                }

                // UPDATE - Var olan poliçeyi güncelle
                existingPolice.PoliceTuru = request.PoliceTuru;
                existingPolice.Plaka = request.Plaka ?? string.Empty;
                existingPolice.TanzimTarihi = request.TanzimTarihi;
                existingPolice.BaslangicTarihi = request.BaslangicTarihi;
                existingPolice.BitisTarihi = request.BitisTarihi;
                existingPolice.BrutPrim = request.BrutPrim;
                existingPolice.NetPrim = request.NetPrim;
                existingPolice.SigortaliAdi = request.SigortaliAdi;
                existingPolice.ProduktorId = request.ProduktorId ?? 0;
                existingPolice.ProduktorSubeId = produktorSubeId;
                existingPolice.UyeId = uyeId;
                existingPolice.MusteriId = request.MusteriId;
                existingPolice.CepTelefonu = request.CepTelefonu;
                existingPolice.GuncelleyenUyeId = _currentUserService.UyeId;
                existingPolice.DisPolice = disPolice; // Acentelik kontrolünden gelen değer
                existingPolice.AcenteAdi = request.AcenteAdi;
                existingPolice.AcenteNo = request.AcenteNo ?? string.Empty;
                existingPolice.GuncellenmeTarihi = _dateTimeService.Now;
                existingPolice.Aciklama = request.Aciklama;

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateYakalananPoliceResult
                {
                    Success = true,
                    Id = existingPolice.Id,
                    IsUpdated = true
                };
            }
            else
            {
                // INSERT - Yeni poliçe oluştur
                var entity = new Domain.Entities.YakalananPolice
                {
                    SigortaSirketi = request.SigortaSirketi,
                    PoliceTuru = request.PoliceTuru,
                    PoliceNumarasi = request.PoliceNumarasi,
                    Plaka = request.Plaka ?? string.Empty,
                    TanzimTarihi = request.TanzimTarihi,
                    BaslangicTarihi = request.BaslangicTarihi,
                    BitisTarihi = request.BitisTarihi,
                    BrutPrim = request.BrutPrim,
                    NetPrim = request.NetPrim,
                    SigortaliAdi = request.SigortaliAdi,
                    ProduktorId = request.ProduktorId ?? 0,
                    ProduktorSubeId = produktorSubeId,
                    UyeId = uyeId,
                    SubeId = subeId,
                    FirmaId = firmaId,
                    MusteriId = request.MusteriId,
                    CepTelefonu = request.CepTelefonu,
                    GuncelleyenUyeId = null,
                    DisPolice = disPolice, // Acentelik kontrolünden gelen değer
                    AcenteAdi = request.AcenteAdi,
                    AcenteNo = request.AcenteNo ?? string.Empty,
                    EklenmeTarihi = _dateTimeService.Now,
                    GuncellenmeTarihi = null,
                    Aciklama = request.Aciklama,
                    IsDeleted = 0
                };

                _context.YakalananPoliceler.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);

                return new CreateYakalananPoliceResult
                {
                    Success = true,
                    Id = entity.Id,
                    IsUpdated = false
                };
            }
        }
        catch (Exception ex)
        {
            return new CreateYakalananPoliceResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}
