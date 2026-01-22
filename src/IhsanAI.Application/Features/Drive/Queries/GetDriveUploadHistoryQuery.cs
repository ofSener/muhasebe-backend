using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IhsanAI.Application.Features.Drive.Queries;

public record GetDriveUploadHistoryQuery(int Page = 1, int PageSize = 20) : IRequest<DriveUploadHistoryResponse>;

public record DriveUploadHistoryResponse(
    List<DriveUploadLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record DriveUploadLogDto(
    int Id,
    string FileName,
    string? DrivePath,
    long FileSizeBytes,
    string FileSizeFormatted,
    string Status,
    string? ErrorMessage,
    DateTime UploadedAt
);

public class GetDriveUploadHistoryQueryHandler : IRequestHandler<GetDriveUploadHistoryQuery, DriveUploadHistoryResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDriveUploadHistoryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DriveUploadHistoryResponse> Handle(GetDriveUploadHistoryQuery request, CancellationToken cancellationToken)
    {
        var firmaId = _currentUserService.FirmaId
            ?? throw new ForbiddenAccessException("Firma bilgisi bulunamadi.");

        var query = _context.DriveUploadLogs
            .AsNoTracking()
            .Where(l => l.FirmaId == firmaId)
            .OrderByDescending(l => l.UploadedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new DriveUploadLogDto(
                l.Id,
                l.FileName,
                l.DriveFolderPath,
                l.FileSizeBytes,
                FormatBytes(l.FileSizeBytes),
                l.UploadStatus.ToString(),
                l.ErrorMessage,
                l.UploadedAt
            ))
            .ToListAsync(cancellationToken);

        return new DriveUploadHistoryResponse(items, totalCount, request.Page, request.PageSize);
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
