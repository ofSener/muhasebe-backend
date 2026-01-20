using MediatR;
using IhsanAI.Application.Common.Interfaces;
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
    string? AcenteliklereGorePoliceYakalansin
) : IRequest<Yetki>;

public class CreateYetkiCommandHandler : IRequestHandler<CreateYetkiCommand, Yetki>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CreateYetkiCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<Yetki> Handle(CreateYetkiCommand request, CancellationToken cancellationToken)
    {
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
            KayitTarihi = _dateTimeService.Now,
            GuncellemeTarihi = _dateTimeService.Now
        };

        _context.Yetkiler.Add(yetki);
        await _context.SaveChangesAsync(cancellationToken);

        return yetki;
    }
}
