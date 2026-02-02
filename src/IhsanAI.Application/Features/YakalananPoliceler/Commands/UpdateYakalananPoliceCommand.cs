using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.YakalananPoliceler.Commands;

/// <summary>
/// Yakalanan poliçeyi günceller (Prodüktör/Şube atama)
/// </summary>
public record UpdateYakalananPoliceCommand : IRequest<UpdateYakalananPoliceResult>
{
    public int Id { get; init; }
    public int ProduktorId { get; init; }
    public int SubeId { get; init; }
}

public record UpdateYakalananPoliceResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public class UpdateYakalananPoliceCommandHandler : IRequestHandler<UpdateYakalananPoliceCommand, UpdateYakalananPoliceResult>
{
    private readonly IApplicationDbContext _context;

    public UpdateYakalananPoliceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UpdateYakalananPoliceResult> Handle(UpdateYakalananPoliceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Yakalanan poliçeyi bul
            var police = await _context.YakalananPoliceler
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (police == null)
            {
                return new UpdateYakalananPoliceResult
                {
                    Success = false,
                    Message = "Poliçe bulunamadı"
                };
            }

            // Prodüktör ve Şube güncelle
            police.ProduktorId = request.ProduktorId;
            police.SubeId = request.SubeId;
            police.GuncellenmeTarihi = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return new UpdateYakalananPoliceResult
            {
                Success = true,
                Message = "Poliçe başarıyla güncellendi"
            };
        }
        catch (Exception ex)
        {
            return new UpdateYakalananPoliceResult
            {
                Success = false,
                Message = $"Güncelleme hatası: {ex.Message}"
            };
        }
    }
}
