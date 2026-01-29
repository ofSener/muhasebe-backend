using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Auth.Queries;

/// <summary>
/// Kullanıcının aktif oturum bilgilerini getirir
/// </summary>
public record GetActiveSessionsQuery(int KullaniciId) : IRequest<List<ActiveSessionDto>>;

public record ActiveSessionDto
{
    public int Id { get; init; }
    public string? DeviceInfo { get; init; }
    public string? IpAddress { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public DateTime RefreshTokenExpiry { get; init; }
    public bool IsCurrentSession { get; init; }
}

public class GetActiveSessionsQueryHandler : IRequestHandler<GetActiveSessionsQuery, List<ActiveSessionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public GetActiveSessionsQueryHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<List<ActiveSessionDto>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _context.MuhasebeKullaniciTokens
            .Where(t =>
                t.KullaniciId == request.KullaniciId &&
                t.IsActive &&
                !t.IsRevoked &&
                t.RefreshTokenExpiry > _dateTimeService.Now)
            .OrderByDescending(t => t.LastUsedAt ?? t.CreatedAt)
            .Select(t => new ActiveSessionDto
            {
                Id = t.Id,
                DeviceInfo = t.DeviceInfo,
                IpAddress = t.IpAddress,
                CreatedAt = t.CreatedAt,
                LastUsedAt = t.LastUsedAt,
                RefreshTokenExpiry = t.RefreshTokenExpiry,
                IsCurrentSession = false // Frontend'de current token ile karşılaştırılacak
            })
            .ToListAsync(cancellationToken);

        return sessions;
    }
}
