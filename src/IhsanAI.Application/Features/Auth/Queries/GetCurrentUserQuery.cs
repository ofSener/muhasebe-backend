using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Auth.Commands;

namespace IhsanAI.Application.Features.Auth.Queries;

public record GetCurrentUserQuery(int UserId) : IRequest<UserDto?>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto?>
{
    private readonly IApplicationDbContext _context;

    public GetCurrentUserQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(k => k.Id == request.UserId, cancellationToken);

        if (kullanici == null)
            return null;

        // Get permissions from yetki table (if exists)
        var yetki = kullanici.MuhasebeYetkiId.HasValue
            ? await _context.Yetkiler.FirstOrDefaultAsync(y => y.Id == kullanici.MuhasebeYetkiId, cancellationToken)
            : null;

        // Get firma and sube names
        var firma = kullanici.FirmaId.HasValue
            ? await _context.Firmalar.FirstOrDefaultAsync(f => f.Id == kullanici.FirmaId, cancellationToken)
            : null;

        var sube = kullanici.SubeId.HasValue
            ? await _context.Subeler.FirstOrDefaultAsync(s => s.Id == kullanici.SubeId, cancellationToken)
            : null;

        // Determine permissions based on AnaYoneticimi status
        PermissionsDto? permissions;
        if (kullanici.AnaYoneticimi == 0)
        {
            // Ana Yönetici - full permissions
            permissions = GetFullPermissions();
        }
        else if (yetki != null)
        {
            // Normal user with yetki record
            permissions = new PermissionsDto
            {
                // Poliçe Yetkileri
                GorebilecegiPolicelerveKartlar = yetki.GorebilecegiPolicelerveKartlar,
                PoliceDuzenleyebilsin = yetki.PoliceDuzenleyebilsin,
                PoliceHavuzunuGorebilsin = yetki.PoliceHavuzunuGorebilsin,
                PoliceAktarabilsin = yetki.PoliceAktarabilsin,
                PoliceDosyalarinaErisebilsin = yetki.PoliceDosyalarinaErisebilsin,
                PoliceYakalamaSecenekleri = yetki.PoliceYakalamaSecenekleri,
                // Yönetim Yetkileri
                YetkilerSayfasindaIslemYapabilsin = yetki.YetkilerSayfasindaIslemYapabilsin,
                AcenteliklerSayfasindaIslemYapabilsin = yetki.AcenteliklerSayfasindaIslemYapabilsin,
                KomisyonOranlariniDuzenleyebilsin = yetki.KomisyonOranlariniDuzenleyebilsin,
                ProduktorleriGorebilsin = yetki.ProduktorleriGorebilsin,
                AcenteliklereGorePoliceYakalansin = yetki.AcenteliklereGorePoliceYakalansin,
                // Müşteri Yetkileri
                MusterileriGorebilsin = yetki.MusterileriGorebilsin,
                MusteriListesiGorebilsin = yetki.MusteriListesiGorebilsin,
                MusteriDetayGorebilsin = yetki.MusteriDetayGorebilsin,
                YenilemeTakibiGorebilsin = yetki.YenilemeTakibiGorebilsin,
                // Finans Yetkileri
                FinansSayfasiniGorebilsin = yetki.FinansSayfasiniGorebilsin,
                FinansDashboardGorebilsin = yetki.FinansDashboardGorebilsin,
                PoliceOdemeleriGorebilsin = yetki.PoliceOdemeleriGorebilsin,
                TahsilatTakibiGorebilsin = yetki.TahsilatTakibiGorebilsin,
                FinansRaporlariGorebilsin = yetki.FinansRaporlariGorebilsin,
                // Entegrasyon Yetkileri
                DriveEntegrasyonuGorebilsin = yetki.DriveEntegrasyonuGorebilsin
            };
        }
        else
        {
            // No yetki record and not AnaYonetici - null permissions
            permissions = null;
        }

        return new UserDto
        {
            Id = kullanici.Id,
            Name = $"{kullanici.Adi} {kullanici.Soyadi}".Trim(),
            Email = kullanici.Email ?? string.Empty,
            FirmaId = kullanici.FirmaId,
            SubeId = kullanici.SubeId,
            SubeAdi = sube?.SubeAdi,
            ProfilResmi = kullanici.ProfilYolu,
            Permissions = permissions
        };
    }

    private static PermissionsDto GetFullPermissions()
    {
        return new PermissionsDto
        {
            // Policy Permissions
            GorebilecegiPolicelerveKartlar = "1",
            PoliceDuzenleyebilsin = "1",
            PoliceHavuzunuGorebilsin = "1",
            PoliceAktarabilsin = "1",
            PoliceDosyalarinaErisebilsin = "1",
            PoliceYakalamaSecenekleri = "1",

            // Management Permissions
            YetkilerSayfasindaIslemYapabilsin = "1",
            AcenteliklerSayfasindaIslemYapabilsin = "1",
            KomisyonOranlariniDuzenleyebilsin = "1",
            ProduktorleriGorebilsin = "1",
            AcenteliklereGorePoliceYakalansin = "1",

            // Customer Permissions
            MusterileriGorebilsin = "1",
            MusteriListesiGorebilsin = "1",
            MusteriDetayGorebilsin = "1",
            YenilemeTakibiGorebilsin = "1",

            // Finance Permissions
            FinansSayfasiniGorebilsin = "1",
            FinansDashboardGorebilsin = "1",
            PoliceOdemeleriGorebilsin = "1",
            TahsilatTakibiGorebilsin = "1",
            FinansRaporlariGorebilsin = "1",

            // Integration Permissions
            DriveEntegrasyonuGorebilsin = "1"
        };
    }
}
