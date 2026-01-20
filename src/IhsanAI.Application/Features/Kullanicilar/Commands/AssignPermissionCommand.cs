using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;

namespace IhsanAI.Application.Features.Kullanicilar.Commands;

public record AssignPermissionCommand(int KullaniciId, int YetkiId) : IRequest<bool>;

public class AssignPermissionCommandHandler : IRequestHandler<AssignPermissionCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public AssignPermissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(AssignPermissionCommand request, CancellationToken cancellationToken)
    {
        // Kullaniciyi getir
        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(x => x.Id == request.KullaniciId, cancellationToken);

        if (kullanici == null)
            return false;

        // Kullanici firma dogrulamasi
        if (_currentUserService.FirmaId.HasValue && kullanici.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu kullaniciya yetki atama yetkiniz yok. Farkli firmaya ait.");
        }

        // Yetkiyi getir
        var yetki = await _context.Yetkiler
            .FirstOrDefaultAsync(x => x.Id == request.YetkiId, cancellationToken);

        if (yetki == null)
            return false;

        // Yetki firma dogrulamasi
        if (_currentUserService.FirmaId.HasValue && yetki.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu yetkiyi atama yetkiniz yok. Farkli firmaya ait.");
        }

        // Kullaniciya yetkiyi ata
        kullanici.MuhasebeYetkiId = request.YetkiId;
        kullanici.GuncellemeTarihi = _dateTimeService.Now;

        // Kullanıcının token'ını geçersiz kıl - yeniden giriş yapması gerekecek
        kullanici.Token = null;
        kullanici.TokenExpiry = null;
        kullanici.RefreshToken = null;
        kullanici.RefreshTokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public record RemovePermissionCommand(int KullaniciId) : IRequest<bool>;

public class RemovePermissionCommandHandler : IRequestHandler<RemovePermissionCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public RemovePermissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(RemovePermissionCommand request, CancellationToken cancellationToken)
    {
        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(x => x.Id == request.KullaniciId, cancellationToken);

        if (kullanici == null)
            return false;

        // Kullanici firma dogrulamasi
        if (_currentUserService.FirmaId.HasValue && kullanici.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu kullanicinin yetkisini kaldirma yetkiniz yok. Farkli firmaya ait.");
        }

        // Yetkiyi kaldir
        kullanici.MuhasebeYetkiId = null;
        kullanici.GuncellemeTarihi = _dateTimeService.Now;

        // Kullanıcının token'ını geçersiz kıl - yeniden giriş yapması gerekecek
        kullanici.Token = null;
        kullanici.TokenExpiry = null;
        kullanici.RefreshToken = null;
        kullanici.RefreshTokenExpiry = null;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
