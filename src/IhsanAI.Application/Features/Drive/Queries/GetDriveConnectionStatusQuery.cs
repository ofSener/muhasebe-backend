using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IhsanAI.Application.Features.Drive.Queries;

public record GetDriveConnectionStatusQuery() : IRequest<DriveConnectionStatusDto>;

public record DriveConnectionStatusDto(
    bool IsConnected,
    string? ConnectedEmail,
    DateTime? ConnectedAt,
    int SyncedFolders,
    int UploadedFiles,
    string UsedStorage,
    DateTime? LastSyncAt
);

public class GetDriveConnectionStatusQueryHandler : IRequestHandler<GetDriveConnectionStatusQuery, DriveConnectionStatusDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDriveConnectionStatusQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DriveConnectionStatusDto> Handle(GetDriveConnectionStatusQuery request, CancellationToken cancellationToken)
    {
        var firmaId = _currentUserService.FirmaId;
        if (!firmaId.HasValue)
        {
            return new DriveConnectionStatusDto(false, null, null, 0, 0, "0 MB", null);
        }

        var token = await _context.FirmaDriveTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.FirmaId == firmaId && t.IsActive, cancellationToken);

        if (token == null)
        {
            return new DriveConnectionStatusDto(false, null, null, 0, 0, "0 MB", null);
        }

        // Get upload statistics
        var uploadStats = await _context.DriveUploadLogs
            .AsNoTracking()
            .Where(l => l.FirmaId == firmaId && l.UploadStatus == UploadStatus.Success)
            .GroupBy(l => 1)
            .Select(g => new
            {
                Count = g.Count(),
                TotalSize = g.Sum(l => l.FileSizeBytes),
                LastUpload = g.Max(l => l.UploadedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var folderCount = await _context.DriveUploadLogs
            .AsNoTracking()
            .Where(l => l.FirmaId == firmaId && l.UploadStatus == UploadStatus.Success)
            .Select(l => l.DriveFolderPath)
            .Distinct()
            .CountAsync(cancellationToken);

        return new DriveConnectionStatusDto(
            IsConnected: true,
            ConnectedEmail: token.GoogleEmail,
            ConnectedAt: token.CreatedAt,
            SyncedFolders: folderCount,
            UploadedFiles: uploadStats?.Count ?? 0,
            UsedStorage: FormatBytes(uploadStats?.TotalSize ?? 0),
            LastSyncAt: uploadStats?.LastUpload
        );
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
