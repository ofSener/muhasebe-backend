using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Yetkiler.Commands;

public record CreateYetkiCommand(
    int FirmaId,
    int EkleyenUyeId,
    string YetkiAdi,
    string? GorebilecegiPolicelerveKartlar,
    string? PoliceYakalamaSecenekleri,
    string? ProduktorleriGorebilsin,
    string? PoliceDuzenleyebilsin,
    string? PoliceDosyalarinaErisebilsin,
    string? PoliceAktarabilsin,
    string? PoliceHavuzunuGorebilsin,
    string? YetkilerSayfasindaIslemYapabilsin,
    string? AcenteliklerSayfasindaIslemYapabilsin,
    string? KomisyonOranlariniDuzenleyebilsin,
    string? AcenteliklereGorePoliceYakalansin,
    string? MusterileriGorebilsin,
    string? FinansSayfasiniGorebilsin,
    // Müşterilerimiz Alt Yetkileri
    string? MusteriListesiGorebilsin,
    string? MusteriDetayGorebilsin,
    string? YenilemeTakibiGorebilsin,
    // Finans Alt Yetkileri
    string? FinansDashboardGorebilsin,
    string? PoliceOdemeleriGorebilsin,
    string? TahsilatTakibiGorebilsin,
    string? FinansRaporlariGorebilsin,
    // Entegrasyon Yetkileri
    string? DriveEntegrasyonuGorebilsin
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
