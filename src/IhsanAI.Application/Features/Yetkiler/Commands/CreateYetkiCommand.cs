using System.Text.Json.Serialization;
using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Yetkiler.Commands;

public record CreateYetkiCommand(
    [property: JsonPropertyName("firmaId")] int FirmaId,
    [property: JsonPropertyName("ekleyenUyeId")] int EkleyenUyeId,
    [property: JsonPropertyName("yetkiAdi")] string YetkiAdi,
    [property: JsonPropertyName("gorebilecegiPolicelerveKartlar")] string? GorebilecegiPolicelerveKartlar,
    [property: JsonPropertyName("policeYakalamaSecenekleri")] string? PoliceYakalamaSecenekleri,
    [property: JsonPropertyName("produktorleriGorebilsin")] string? ProduktorleriGorebilsin,
    [property: JsonPropertyName("policeDuzenleyebilsin")] string? PoliceDuzenleyebilsin,
    [property: JsonPropertyName("policeDosyalarinaErisebilsin")] string? PoliceDosyalarinaErisebilsin,
    [property: JsonPropertyName("policeAktarabilsin")] string? PoliceAktarabilsin,
    [property: JsonPropertyName("policeHavuzunuGorebilsin")] string? PoliceHavuzunuGorebilsin,
    [property: JsonPropertyName("yetkilerSayfasindaIslemYapabilsin")] string? YetkilerSayfasindaIslemYapabilsin,
    [property: JsonPropertyName("acenteliklerSayfasindaIslemYapabilsin")] string? AcenteliklerSayfasindaIslemYapabilsin,
    [property: JsonPropertyName("komisyonOranlariniDuzenleyebilsin")] string? KomisyonOranlariniDuzenleyebilsin,
    [property: JsonPropertyName("acenteliklereGorePoliceYakalansin")] string? AcenteliklereGorePoliceYakalansin,
    [property: JsonPropertyName("musterileriGorebilsin")] string? MusterileriGorebilsin,
    [property: JsonPropertyName("finansSayfasiniGorebilsin")] string? FinansSayfasiniGorebilsin,
    // Müşterilerimiz Alt Yetkileri
    [property: JsonPropertyName("musteriListesiGorebilsin")] string? MusteriListesiGorebilsin,
    [property: JsonPropertyName("musteriDetayGorebilsin")] string? MusteriDetayGorebilsin,
    [property: JsonPropertyName("yenilemeTakibiGorebilsin")] string? YenilemeTakibiGorebilsin,
    // Finans Alt Yetkileri
    [property: JsonPropertyName("finansDashboardGorebilsin")] string? FinansDashboardGorebilsin,
    [property: JsonPropertyName("policeOdemeleriGorebilsin")] string? PoliceOdemeleriGorebilsin,
    [property: JsonPropertyName("tahsilatTakibiGorebilsin")] string? TahsilatTakibiGorebilsin,
    [property: JsonPropertyName("finansRaporlariGorebilsin")] string? FinansRaporlariGorebilsin,
    // Entegrasyon Yetkileri
    [property: JsonPropertyName("driveEntegrasyonuGorebilsin")] string? DriveEntegrasyonuGorebilsin
) : IRequest<Yetki>;

public class CreateYetkiCommandHandler : IRequestHandler<CreateYetkiCommand, Yetki>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICurrentUserService _currentUserService;

    public CreateYetkiCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _currentUserService = currentUserService;
    }

    public async Task<Yetki> Handle(CreateYetkiCommand request, CancellationToken cancellationToken)
    {
        // Firma doğrulaması: Kullanıcı sadece kendi firmasının yetkisini oluşturabilir
        if (_currentUserService.FirmaId.HasValue && request.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu firma için yetki oluşturma yetkiniz yok.");
        }

        var yetki = new Yetki
        {
            FirmaId = request.FirmaId,
            EkleyenUyeId = request.EkleyenUyeId,
            YetkiAdi = request.YetkiAdi,
            GorebilecegiPolicelerveKartlar = request.GorebilecegiPolicelerveKartlar,
            PoliceYakalamaSecenekleri = request.PoliceYakalamaSecenekleri,
            ProduktorleriGorebilsin = request.ProduktorleriGorebilsin,
            PoliceDuzenleyebilsin = request.PoliceDuzenleyebilsin,
            PoliceDosyalarinaErisebilsin = request.PoliceDosyalarinaErisebilsin,
            PoliceAktarabilsin = request.PoliceAktarabilsin,
            PoliceHavuzunuGorebilsin = request.PoliceHavuzunuGorebilsin,
            YetkilerSayfasindaIslemYapabilsin = request.YetkilerSayfasindaIslemYapabilsin,
            AcenteliklerSayfasindaIslemYapabilsin = request.AcenteliklerSayfasindaIslemYapabilsin,
            KomisyonOranlariniDuzenleyebilsin = request.KomisyonOranlariniDuzenleyebilsin,
            AcenteliklereGorePoliceYakalansin = request.AcenteliklereGorePoliceYakalansin,
            MusterileriGorebilsin = request.MusterileriGorebilsin,
            FinansSayfasiniGorebilsin = request.FinansSayfasiniGorebilsin,
            // Müşterilerimiz Alt Yetkileri
            MusteriListesiGorebilsin = request.MusteriListesiGorebilsin,
            MusteriDetayGorebilsin = request.MusteriDetayGorebilsin,
            YenilemeTakibiGorebilsin = request.YenilemeTakibiGorebilsin,
            // Finans Alt Yetkileri
            FinansDashboardGorebilsin = request.FinansDashboardGorebilsin,
            PoliceOdemeleriGorebilsin = request.PoliceOdemeleriGorebilsin,
            TahsilatTakibiGorebilsin = request.TahsilatTakibiGorebilsin,
            FinansRaporlariGorebilsin = request.FinansRaporlariGorebilsin,
            // Entegrasyon Yetkileri
            DriveEntegrasyonuGorebilsin = request.DriveEntegrasyonuGorebilsin,
            KayitTarihi = _dateTimeService.Now,
            GuncellemeTarihi = _dateTimeService.Now
        };

        _context.Yetkiler.Add(yetki);
        await _context.SaveChangesAsync(cancellationToken);

        return yetki;
    }
}
