using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Kullanicilar.Commands;

public record UpdateKullaniciYetkiCommand(int KullaniciId, int? MuhasebeYetkiId) : IRequest<bool>;

public class UpdateKullaniciYetkiCommandHandler : IRequestHandler<UpdateKullaniciYetkiCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateKullaniciYetkiCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateKullaniciYetkiCommand request, CancellationToken cancellationToken)
    {
        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(x => x.Id == request.KullaniciId, cancellationToken);

        if (kullanici == null)
            return false;

        kullanici.MuhasebeYetkiId = request.MuhasebeYetkiId;
        kullanici.GuncellemeTarihi = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
