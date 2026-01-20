using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Yetkiler.Commands;

public record UpdateYetkiCommand(
    int Id,
    string? YetkiAdi,
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
    string? FinansRaporlariGorebilsin
) : IRequest<Yetki?>;

public class UpdateYetkiCommandHandler : IRequestHandler<UpdateYetkiCommand, Yetki?>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateYetkiCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _currentUserService = currentUserService;
    }

    public async Task<Yetki?> Handle(UpdateYetkiCommand request, CancellationToken cancellationToken)
    {
        var yetki = await _context.Yetkiler
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (yetki == null)
            return null;

        // Firma doğrulaması: Kullanıcı sadece kendi firmasının yetkisini güncelleyebilir
        if (_currentUserService.FirmaId.HasValue && yetki.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu firma için yetki güncelleme yetkiniz yok.");
        }

        // Update fields if provided
        if (request.YetkiAdi != null)
            yetki.YetkiAdi = request.YetkiAdi;

        if (request.GorebilecegiPolicelerveKartlar != null)
            yetki.GorebilecegiPolicelerveKartlar = request.GorebilecegiPolicelerveKartlar;

        if (request.PoliceYakalamaSecenekleri != null)
            yetki.PoliceYakalamaSecenekleri = request.PoliceYakalamaSecenekleri;

        if (request.ProduktorleriGorebilsin != null)
            yetki.ProduktorleriGorebilsin = request.ProduktorleriGorebilsin;

        if (request.PoliceDuzenleyebilsin != null)
            yetki.PoliceDuzenleyebilsin = request.PoliceDuzenleyebilsin;

        if (request.PoliceDosyalarinaErisebilsin != null)
            yetki.PoliceDosyalarinaErisebilsin = request.PoliceDosyalarinaErisebilsin;

        if (request.PoliceAktarabilsin != null)
            yetki.PoliceAktarabilsin = request.PoliceAktarabilsin;

        if (request.PoliceHavuzunuGorebilsin != null)
            yetki.PoliceHavuzunuGorebilsin = request.PoliceHavuzunuGorebilsin;

        if (request.YetkilerSayfasindaIslemYapabilsin != null)
            yetki.YetkilerSayfasindaIslemYapabilsin = request.YetkilerSayfasindaIslemYapabilsin;

        if (request.AcenteliklerSayfasindaIslemYapabilsin != null)
            yetki.AcenteliklerSayfasindaIslemYapabilsin = request.AcenteliklerSayfasindaIslemYapabilsin;

        if (request.KomisyonOranlariniDuzenleyebilsin != null)
            yetki.KomisyonOranlariniDuzenleyebilsin = request.KomisyonOranlariniDuzenleyebilsin;

        if (request.AcenteliklereGorePoliceYakalansin != null)
            yetki.AcenteliklereGorePoliceYakalansin = request.AcenteliklereGorePoliceYakalansin;

        if (request.MusterileriGorebilsin != null)
            yetki.MusterileriGorebilsin = request.MusterileriGorebilsin;

        if (request.FinansSayfasiniGorebilsin != null)
            yetki.FinansSayfasiniGorebilsin = request.FinansSayfasiniGorebilsin;

        // Müşterilerimiz Alt Yetkileri
        if (request.MusteriListesiGorebilsin != null)
            yetki.MusteriListesiGorebilsin = request.MusteriListesiGorebilsin;

        if (request.MusteriDetayGorebilsin != null)
            yetki.MusteriDetayGorebilsin = request.MusteriDetayGorebilsin;

        if (request.YenilemeTakibiGorebilsin != null)
            yetki.YenilemeTakibiGorebilsin = request.YenilemeTakibiGorebilsin;

        // Finans Alt Yetkileri
        if (request.FinansDashboardGorebilsin != null)
            yetki.FinansDashboardGorebilsin = request.FinansDashboardGorebilsin;

        if (request.PoliceOdemeleriGorebilsin != null)
            yetki.PoliceOdemeleriGorebilsin = request.PoliceOdemeleriGorebilsin;

        if (request.TahsilatTakibiGorebilsin != null)
            yetki.TahsilatTakibiGorebilsin = request.TahsilatTakibiGorebilsin;

        if (request.FinansRaporlariGorebilsin != null)
            yetki.FinansRaporlariGorebilsin = request.FinansRaporlariGorebilsin;

        yetki.GuncellemeTarihi = _dateTimeService.Now;

        await _context.SaveChangesAsync(cancellationToken);

        // Bu yetkiye sahip tüm kullanıcıların token'larını geçersiz kıl
        // Böylece yeniden giriş yapmaları gerekir ve yeni yetkiler uygulanır
        var affectedUsers = await _context.Kullanicilar
            .Where(k => k.MuhasebeYetkiId == request.Id && k.Token != null)
            .ToListAsync(cancellationToken);

        foreach (var user in affectedUsers)
        {
            user.Token = null;
            user.TokenExpiry = null;
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
        }

        if (affectedUsers.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return yetki;
    }
}
