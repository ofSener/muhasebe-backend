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

        // Get permissions
        var yetki = kullanici.MuhasebeYetkiId.HasValue
            ? await _context.Yetkiler.FirstOrDefaultAsync(y => y.Id == kullanici.MuhasebeYetkiId, cancellationToken)
            : null;

        // Determine role
        var role = yetki?.GorebilecegiPolicelerveKartlar switch
        {
            "1" => "admin",
            "2" => "editor",
            "3" => "viewer",
            _ => "viewer"
        };

        // Get firma and sube names
        var firma = kullanici.FirmaId.HasValue
            ? await _context.Firmalar.FirstOrDefaultAsync(f => f.Id == kullanici.FirmaId, cancellationToken)
            : null;

        var sube = kullanici.SubeId.HasValue
            ? await _context.Subeler.FirstOrDefaultAsync(s => s.Id == kullanici.SubeId, cancellationToken)
            : null;

        return new UserDto
        {
            Id = kullanici.Id,
            Name = $"{kullanici.Adi} {kullanici.Soyadi}".Trim(),
            Email = kullanici.Email ?? string.Empty,
            Role = role,
            FirmaId = kullanici.FirmaId,
            SubeId = kullanici.SubeId,
            ProfilResmi = kullanici.ProfilYolu,
            Permissions = yetki != null ? new PermissionsDto
            {
                GorebilecegiPoliceler = yetki.GorebilecegiPolicelerveKartlar,
                PoliceDuzenleyebilsin = yetki.PoliceDuzenleyebilsin,
                PoliceHavuzunuGorebilsin = yetki.PoliceHavuzunuGorebilsin,
                YetkilerSayfasindaIslemYapabilsin = yetki.YetkilerSayfasindaIslemYapabilsin,
                AcenteliklerSayfasindaIslemYapabilsin = yetki.AcenteliklerSayfasindaIslemYapabilsin,
                KomisyonOranlariniDuzenleyebilsin = yetki.KomisyonOranlariniDuzenleyebilsin,
                ProduktorleriGorebilsin = yetki.ProduktorleriGorebilsin,
                PoliceAktarabilsin = yetki.PoliceAktarabilsin
            } : null
        };
    }
}
