using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IhsanAI.Application.Features.Drive.Commands;

public record UploadFileToDriveCommand(IFormFile File) : IRequest<DriveUploadResponse>;

public record DriveUploadResponse(
    bool Success,
    string? FileId,
    string? WebViewLink,
    string? DrivePath,
    string? ErrorMessage
);

public class UploadFileToDriveCommandHandler : IRequestHandler<UploadFileToDriveCommand, DriveUploadResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IGoogleDriveService _driveService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    public UploadFileToDriveCommandHandler(
        IApplicationDbContext context,
        IGoogleDriveService driveService,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _driveService = driveService;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<DriveUploadResponse> Handle(UploadFileToDriveCommand request, CancellationToken cancellationToken)
    {
        var firmaId = _currentUserService.FirmaId
            ?? throw new ForbiddenAccessException("Firma bilgisi bulunamadi.");

        // Validate PDF only
        if (!request.File.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !request.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new DriveUploadResponse(false, null, null, null, "Sadece PDF dosyalari yuklenebilir.");
        }

        // Validate file size
        if (request.File.Length > MaxFileSizeBytes)
        {
            return new DriveUploadResponse(false, null, null, null, "Dosya boyutu 10MB'i asamaz.");
        }

        // Check if firm has active Drive connection
        var driveToken = await _context.FirmaDriveTokens
            .FirstOrDefaultAsync(t => t.FirmaId == firmaId && t.IsActive, cancellationToken);

        if (driveToken == null)
        {
            return new DriveUploadResponse(false, null, null, null, "Google Drive baglantisi bulunamadi. Lutfen once Drive'a baglanin.");
        }

        var now = _dateTimeService.Now;
        var drivePath = $"/IHSAN AI/{now.Year}/{now.Month:D2}/{request.File.FileName}";

        // Create log entry
        var uploadLog = new DriveUploadLog
        {
            FirmaId = firmaId,
            FileName = request.File.FileName,
            OriginalFileName = request.File.FileName,
            FileSizeBytes = request.File.Length,
            DriveFolderPath = drivePath,
            UploadStatus = UploadStatus.Pending,
            UploadedByUserId = int.TryParse(_currentUserService.UserId, out var userId) ? userId : null,
            UploadedAt = now
        };

        _context.DriveUploadLogs.Add(uploadLog);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            using var stream = request.File.OpenReadStream();
            var result = await _driveService.UploadFileAsync(
                firmaId,
                stream,
                request.File.FileName,
                "application/pdf"
            );

            uploadLog.UploadStatus = result.Success ? UploadStatus.Success : UploadStatus.Failed;
            uploadLog.DriveFileId = result.FileId;
            uploadLog.DriveWebViewLink = result.WebViewLink;
            uploadLog.ErrorMessage = result.ErrorMessage;
            await _context.SaveChangesAsync(cancellationToken);

            return new DriveUploadResponse(
                result.Success,
                result.FileId,
                result.WebViewLink,
                drivePath,
                result.ErrorMessage
            );
        }
        catch (Exception ex)
        {
            uploadLog.UploadStatus = UploadStatus.Failed;
            uploadLog.ErrorMessage = ex.Message;
            await _context.SaveChangesAsync(cancellationToken);

            return new DriveUploadResponse(false, null, null, null, $"Yukleme hatasi: {ex.Message}");
        }
    }
}
