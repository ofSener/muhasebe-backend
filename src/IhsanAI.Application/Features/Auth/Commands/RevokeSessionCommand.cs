using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Auth.Commands;

/// <summary>
/// Belirli bir session'ı (token) iptal eder
/// </summary>
public record RevokeSessionCommand(
    int KullaniciId,
    int SessionId
) : IRequest<RevokeSessionResponse>;

public record RevokeSessionResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, RevokeSessionResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public RevokeSessionCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<RevokeSessionResponse> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        // Session'ı bul ve kullanıcıya ait olduğunu doğrula
        var session = await _context.MuhasebeKullaniciTokens
            .FirstOrDefaultAsync(t =>
                t.Id == request.SessionId &&
                t.KullaniciId == request.KullaniciId &&
                t.IsActive &&
                !t.IsRevoked,
                cancellationToken);

        if (session == null)
        {
            return new RevokeSessionResponse
            {
                Success = false,
                Message = "Oturum bulunamadı veya zaten sonlandırılmış"
            };
        }

        // OPTIMIZASYON: Session'ı revoke etmek yerine database'den sil
        // Manuel sonlandırılan token'a ihtiyaç yok
        _context.MuhasebeKullaniciTokens.Remove(session);
        await _context.SaveChangesAsync(cancellationToken);

        return new RevokeSessionResponse
        {
            Success = true,
            Message = "Oturum başarıyla sonlandırıldı"
        };
    }
}
