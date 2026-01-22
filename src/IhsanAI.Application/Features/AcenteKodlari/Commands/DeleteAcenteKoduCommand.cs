using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;

namespace IhsanAI.Application.Features.AcenteKodlari.Commands;

public record DeleteAcenteKoduCommand(int Id) : IRequest<bool>;

public class DeleteAcenteKoduCommandHandler : IRequestHandler<DeleteAcenteKoduCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteAcenteKoduCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(DeleteAcenteKoduCommand request, CancellationToken cancellationToken)
    {
        var acenteKodu = await _context.AcenteKodlari
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (acenteKodu == null)
            return false;

        // Firma doğrulaması: Kullanıcı sadece kendi firmasının acente kodunu silebilir
        if (_currentUserService.FirmaId.HasValue && acenteKodu.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu firma için acente kodu silme yetkiniz yok.");
        }

        _context.AcenteKodlari.Remove(acenteKodu);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
