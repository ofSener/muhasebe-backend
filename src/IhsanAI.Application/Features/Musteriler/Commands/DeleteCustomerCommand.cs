using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;

namespace IhsanAI.Application.Features.Musteriler.Commands;

public record DeleteCustomerCommand(int Id) : IRequest<DeleteCustomerResultDto>;

public record DeleteCustomerResultDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, DeleteCustomerResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteCustomerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DeleteCustomerResultDto> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var musteri = await _context.Musteriler
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (musteri == null)
        {
            return new DeleteCustomerResultDto
            {
                Success = false,
                ErrorMessage = "Müşteri bulunamadı."
            };
        }

        // Firma doğrulaması: Kullanıcı sadece kendi firmasının müşterisini silebilir
        if (_currentUserService.FirmaId.HasValue && musteri.EkleyenFirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu müşteriyi silme yetkiniz yok.");
        }

        // Check if customer has policies
        var hasPolicies = await _context.Policeler
            .AnyAsync(p => p.MusteriId == request.Id, cancellationToken);

        if (hasPolicies)
        {
            return new DeleteCustomerResultDto
            {
                Success = false,
                ErrorMessage = "Bu müşteriye ait poliçeler bulunduğu için silinemez."
            };
        }

        _context.Musteriler.Remove(musteri);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteCustomerResultDto
        {
            Success = true
        };
    }
}
