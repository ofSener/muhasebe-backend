using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IhsanAI.Application.Features.Drive.Commands;

public record DisconnectDriveCommand() : IRequest<bool>;

public class DisconnectDriveCommandHandler : IRequestHandler<DisconnectDriveCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public DisconnectDriveCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(DisconnectDriveCommand request, CancellationToken cancellationToken)
    {
        var firmaId = _currentUserService.FirmaId
            ?? throw new ForbiddenAccessException("Firma bilgisi bulunamadi.");

        var token = await _context.FirmaDriveTokens
            .FirstOrDefaultAsync(t => t.FirmaId == firmaId, cancellationToken);

        if (token == null) return true;

        token.IsActive = false;
        token.UpdatedAt = _dateTimeService.Now;
        token.UpdatedBy = _currentUserService.UserName;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
