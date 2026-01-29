using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Auth.Commands;

/// <summary>
/// Süresi dolmuş ve iptal edilmiş token'ları temizler
/// </summary>
public record CleanupExpiredTokensCommand(int? DaysOld = 30) : IRequest<CleanupExpiredTokensResponse>;

public record CleanupExpiredTokensResponse
{
    public bool Success { get; init; }
    public int DeletedCount { get; init; }
    public string? Message { get; init; }
}

public class CleanupExpiredTokensCommandHandler : IRequestHandler<CleanupExpiredTokensCommand, CleanupExpiredTokensResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public CleanupExpiredTokensCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<CleanupExpiredTokensResponse> Handle(CleanupExpiredTokensCommand request, CancellationToken cancellationToken)
    {
        var daysOld = request.DaysOld ?? 30;
        var cutoffDate = _dateTimeService.Now.AddDays(-daysOld);

        // Temizlenecek token'ları bul:
        // 1. Refresh token süresi dolmuş olanlar
        // 2. Revoke edilmiş ve X gün geçmiş olanlar
        var tokensToDelete = await _context.MuhasebeKullaniciTokens
            .Where(t =>
                // Süresi dolmuş token'lar
                (t.RefreshTokenExpiry < _dateTimeService.Now) ||
                // veya iptal edilmiş ve eski token'lar
                (t.IsRevoked && t.RevokedAt.HasValue && t.RevokedAt.Value < cutoffDate))
            .ToListAsync(cancellationToken);

        var count = tokensToDelete.Count;

        if (count > 0)
        {
            _context.MuhasebeKullaniciTokens.RemoveRange(tokensToDelete);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new CleanupExpiredTokensResponse
        {
            Success = true,
            DeletedCount = count,
            Message = $"{count} adet eski token temizlendi"
        };
    }
}
