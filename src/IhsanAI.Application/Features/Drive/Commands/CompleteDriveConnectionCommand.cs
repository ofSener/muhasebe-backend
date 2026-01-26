using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IhsanAI.Application.Features.Drive.Commands;

public record CompleteDriveConnectionCommand(string Code, string State, string RedirectUri) : IRequest<bool>;

public class CompleteDriveConnectionCommandHandler : IRequestHandler<CompleteDriveConnectionCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleDriveService _driveService;
    private readonly IDateTimeService _dateTimeService;

    public CompleteDriveConnectionCommandHandler(
        IApplicationDbContext context,
        IGoogleDriveService driveService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _driveService = driveService;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(CompleteDriveConnectionCommand request, CancellationToken cancellationToken)
    {
        if (!int.TryParse(request.State, out var firmaId))
            throw new ArgumentException("Gecersiz state parametresi.");

        var tokenResult = await _driveService.ExchangeCodeForTokensAsync(request.Code, request.RedirectUri);

        var existingToken = await _context.FirmaDriveTokens
            .FirstOrDefaultAsync(t => t.FirmaId == firmaId, cancellationToken);

        if (existingToken != null)
        {
            existingToken.AccessToken = tokenResult.AccessToken;
            existingToken.RefreshToken = tokenResult.RefreshToken;
            existingToken.TokenExpiresAt = tokenResult.ExpiresAt;
            existingToken.GoogleEmail = tokenResult.Email;
            existingToken.IsActive = true;
            existingToken.UpdatedAt = _dateTimeService.Now;
        }
        else
        {
            var newToken = new FirmaDriveToken
            {
                FirmaId = firmaId,
                AccessToken = tokenResult.AccessToken,
                RefreshToken = tokenResult.RefreshToken,
                TokenExpiresAt = tokenResult.ExpiresAt,
                GoogleEmail = tokenResult.Email,
                IsActive = true,
                CreatedAt = _dateTimeService.Now
            };
            _context.FirmaDriveTokens.Add(newToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Create root folder structure
        await _driveService.CreateFolderStructureAsync(firmaId, "IHSAN AI");

        return true;
    }
}
