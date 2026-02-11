using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Policeler.Commands;

/// <summary>
/// Mevcut bir poliçeye TC Kimlik No veya Vergi No atar.
/// Müşteri eşleştirmesi otomatik yapılır, gerekirse yeni müşteri oluşturulur.
/// Aynı TC/VKN'ye sahip eşleşmemiş diğer poliçeler de güncellenir.
/// </summary>
public record AssignTcToPolicyCommand : IRequest<AssignTcResult>
{
    public int PolicyId { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
}

public record AssignTcResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? MusteriId { get; init; }
    public bool AutoCreated { get; init; }
    public int CascadeUpdated { get; init; }
}

public class AssignTcToPolicyCommandHandler : IRequestHandler<AssignTcToPolicyCommand, AssignTcResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerMatchingService _customerMatchingService;

    public AssignTcToPolicyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICustomerMatchingService customerMatchingService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _customerMatchingService = customerMatchingService;
    }

    public async Task<AssignTcResult> Handle(AssignTcToPolicyCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TcKimlikNo) && string.IsNullOrWhiteSpace(request.VergiNo))
        {
            return new AssignTcResult { Success = false, ErrorMessage = "TC Kimlik No veya Vergi No girilmelidir." };
        }

        var policy = await _context.Policeler
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

        if (policy == null)
        {
            return new AssignTcResult { Success = false, ErrorMessage = "Poliçe bulunamadı." };
        }

        // TC/VKN'yi poliçeye ata
        if (!string.IsNullOrWhiteSpace(request.TcKimlikNo))
            policy.TcKimlikNo = request.TcKimlikNo.Trim();
        if (!string.IsNullOrWhiteSpace(request.VergiNo))
            policy.VergiNo = request.VergiNo.Trim();

        // Müşteri eşleştirmesi yap
        var matchResult = await _customerMatchingService.FindBestMatchAsync(new CustomerMatchRequest
        {
            TcKimlikNo = request.TcKimlikNo,
            VergiNo = request.VergiNo,
            SigortaliAdi = policy.SigortaliAdi,
            Plaka = policy.Plaka,
            FirmaId = policy.FirmaId
        }, cancellationToken);

        if (matchResult.MusteriId.HasValue)
        {
            policy.MusteriId = matchResult.MusteriId;
        }

        // Kaskad güncelleme: aynı TC/VKN ile eşleşmemiş diğer poliçeleri güncelle
        var cascadeCount = 0;
        if (matchResult.MusteriId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(request.TcKimlikNo))
            {
                var unmatchedByTc = await _context.Policeler
                    .Where(p =>
                        p.TcKimlikNo == request.TcKimlikNo.Trim() &&
                        p.MusteriId == null &&
                        p.FirmaId == policy.FirmaId &&
                        p.Id != policy.Id)
                    .ToListAsync(cancellationToken);

                foreach (var p in unmatchedByTc)
                {
                    p.MusteriId = matchResult.MusteriId;
                    cascadeCount++;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.VergiNo))
            {
                var unmatchedByVkn = await _context.Policeler
                    .Where(p =>
                        p.VergiNo == request.VergiNo.Trim() &&
                        p.MusteriId == null &&
                        p.FirmaId == policy.FirmaId &&
                        p.Id != policy.Id)
                    .ToListAsync(cancellationToken);

                foreach (var p in unmatchedByVkn)
                {
                    p.MusteriId = matchResult.MusteriId;
                    cascadeCount++;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new AssignTcResult
        {
            Success = true,
            MusteriId = matchResult.MusteriId,
            AutoCreated = matchResult.AutoCreated,
            CascadeUpdated = cascadeCount
        };
    }
}
