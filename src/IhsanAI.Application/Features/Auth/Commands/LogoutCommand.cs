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
        // OPTIMIZASYON: Token'ları revoke etmek yerine database'den sil
        // Logout sonrası token'a ihtiyaç yok, kayıt şişmesini önle

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            // Sadece belirli bir token'ı sil
            var tokenRecord = await _context.MuhasebeKullaniciTokens
                .FirstOrDefaultAsync(t =>
                    t.KullaniciId == request.KullaniciId &&
                    t.RefreshToken == request.RefreshToken &&
                    t.IsActive &&
                    !t.IsRevoked,
                    cancellationToken);

            if (tokenRecord != null)
            {
                _context.MuhasebeKullaniciTokens.Remove(tokenRecord);
            }
        }
        else
        {
            // Kullanıcının TÜM aktif token'larını sil (tüm cihazlardan çıkış)
            var activeTokens = await _context.MuhasebeKullaniciTokens
                .Where(t =>
                    t.KullaniciId == request.KullaniciId &&
                    t.IsActive &&
                    !t.IsRevoked)
                .ToListAsync(cancellationToken);

            if (activeTokens.Count > 0)
            {
                _context.MuhasebeKullaniciTokens.RemoveRange(activeTokens);
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
