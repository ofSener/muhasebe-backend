using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Auth.Commands;

/// <summary>
/// Kullanıcı çıkışı yapar ve token'ı revoke eder
/// </summary>
public record LogoutCommand(
    int KullaniciId,
    string? RefreshToken = null
) : IRequest<LogoutResponse>;

public record LogoutResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, LogoutResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public LogoutCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<LogoutResponse> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Eğer refresh token verilmişse sadece o token'ı revoke et
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var tokenRecord = await _context.MuhasebeKullaniciTokens
                .FirstOrDefaultAsync(t =>
                    t.KullaniciId == request.KullaniciId &&
                    t.RefreshToken == request.RefreshToken &&
                    t.IsActive &&
                    !t.IsRevoked,
                    cancellationToken);

            if (tokenRecord != null)
            {
                tokenRecord.IsRevoked = true;
                tokenRecord.RevokedAt = _dateTimeService.Now;
                tokenRecord.RevokeReason = "Kullanıcı çıkış yaptı (logout)";
                tokenRecord.IsActive = false;
            }
        }
        else
        {
            // Refresh token yoksa kullanıcının TÜM aktif token'larını revoke et
            var activeTokens = await _context.MuhasebeKullaniciTokens
                .Where(t =>
                    t.KullaniciId == request.KullaniciId &&
                    t.IsActive &&
                    !t.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = _dateTimeService.Now;
                token.RevokeReason = "Kullanıcı tüm cihazlardan çıkış yaptı";
                token.IsActive = false;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new LogoutResponse
        {
            Success = true,
            Message = "Başarıyla çıkış yapıldı"
        };
    }
}
