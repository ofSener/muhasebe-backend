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

        // Session'ı revoke et
        session.IsRevoked = true;
        session.RevokedAt = _dateTimeService.Now;
        session.RevokeReason = "Kullanıcı tarafından manuel olarak sonlandırıldı";
        session.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);

        return new RevokeSessionResponse
        {
            Success = true,
            Message = "Oturum başarıyla sonlandırıldı"
        };
    }
}
