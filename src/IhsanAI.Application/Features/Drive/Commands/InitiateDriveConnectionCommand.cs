using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;

namespace IhsanAI.Application.Features.Drive.Commands;

public record InitiateDriveConnectionCommand(string RedirectUri) : IRequest<DriveAuthUrlResult>;

public record DriveAuthUrlResult(string AuthorizationUrl, string State);

public class InitiateDriveConnectionCommandHandler : IRequestHandler<InitiateDriveConnectionCommand, DriveAuthUrlResult>
{
    private readonly IGoogleDriveService _driveService;
    private readonly ICurrentUserService _currentUserService;

    public InitiateDriveConnectionCommandHandler(
        IGoogleDriveService driveService,
        ICurrentUserService currentUserService)
    {
        _driveService = driveService;
        _currentUserService = currentUserService;
    }

    public async Task<DriveAuthUrlResult> Handle(InitiateDriveConnectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.FirmaId.HasValue)
            throw new ForbiddenAccessException("Firma bilgisi bulunamadi.");

        var authUrl = await _driveService.GetAuthorizationUrlAsync(
            _currentUserService.FirmaId.Value,
            request.RedirectUri
        );

        return new DriveAuthUrlResult(authUrl, _currentUserService.FirmaId.Value.ToString());
    }
}
