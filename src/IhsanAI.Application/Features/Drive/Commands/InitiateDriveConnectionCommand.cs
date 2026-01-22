using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using Microsoft.Extensions.Configuration;

namespace IhsanAI.Application.Features.Drive.Commands;

public record InitiateDriveConnectionCommand() : IRequest<DriveAuthUrlResult>;

public record DriveAuthUrlResult(string AuthorizationUrl, string State);

public class InitiateDriveConnectionCommandHandler : IRequestHandler<InitiateDriveConnectionCommand, DriveAuthUrlResult>
{
    private readonly IGoogleDriveService _driveService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IConfiguration _configuration;

    public InitiateDriveConnectionCommandHandler(
        IGoogleDriveService driveService,
        ICurrentUserService currentUserService,
        IConfiguration configuration)
    {
        _driveService = driveService;
        _currentUserService = currentUserService;
        _configuration = configuration;
    }

    public async Task<DriveAuthUrlResult> Handle(InitiateDriveConnectionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.FirmaId.HasValue)
            throw new ForbiddenAccessException("Firma bilgisi bulunamadi.");

        var redirectUri = _configuration["GoogleDrive:RedirectUri"]
            ?? throw new InvalidOperationException("GoogleDrive:RedirectUri yapilandirmasi eksik.");

        var authUrl = await _driveService.GetAuthorizationUrlAsync(
            _currentUserService.FirmaId.Value,
            redirectUri
        );

        return new DriveAuthUrlResult(authUrl, _currentUserService.FirmaId.Value.ToString());
    }
}
