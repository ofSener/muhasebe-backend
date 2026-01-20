using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;

namespace IhsanAI.Application.Features.Yetkiler.Commands;

public record DeleteYetkiCommand(int Id) : IRequest<bool>;

public class DeleteYetkiCommandHandler : IRequestHandler<DeleteYetkiCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteYetkiCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(DeleteYetkiCommand request, CancellationToken cancellationToken)
    {
        var yetki = await _context.Yetkiler
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (yetki == null)
            return false;

        // Firma doğrulaması: Kullanıcı sadece kendi firmasının yetkisini silebilir
        if (_currentUserService.FirmaId.HasValue && yetki.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu firma için yetki silme yetkiniz yok.");
        }

        _context.Yetkiler.Remove(yetki);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
