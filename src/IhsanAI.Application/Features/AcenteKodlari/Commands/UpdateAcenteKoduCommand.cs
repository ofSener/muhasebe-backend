using System.Text.Json.Serialization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.AcenteKodlari.Commands;

public record UpdateAcenteKoduCommand(
    int Id,
    [property: JsonPropertyName("sigortaSirketiId")] int? SigortaSirketiId = null,
    [property: JsonPropertyName("acenteKoduDeger")] string? AcenteKoduDeger = null,
    [property: JsonPropertyName("acenteAdi")] string? AcenteAdi = null,
    [property: JsonPropertyName("disAcente")] sbyte? DisAcente = null
) : IRequest<AcenteKodu?>;

public class UpdateAcenteKoduCommandHandler : IRequestHandler<UpdateAcenteKoduCommand, AcenteKodu?>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateAcenteKoduCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _currentUserService = currentUserService;
    }

    public async Task<AcenteKodu?> Handle(UpdateAcenteKoduCommand request, CancellationToken cancellationToken)
    {
        var acenteKodu = await _context.AcenteKodlari
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (acenteKodu == null)
            return null;

        // Firma doğrulaması: Kullanıcı sadece kendi firmasının acente kodunu güncelleyebilir
        if (_currentUserService.FirmaId.HasValue && acenteKodu.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu firma için acente kodu güncelleme yetkiniz yok.");
        }

        // Update fields if provided
        if (request.SigortaSirketiId.HasValue)
            acenteKodu.SigortaSirketiId = request.SigortaSirketiId.Value;

        if (request.AcenteKoduDeger != null)
            acenteKodu.AcenteKoduDeger = request.AcenteKoduDeger;

        if (request.AcenteAdi != null)
            acenteKodu.AcenteAdi = request.AcenteAdi;

        if (request.DisAcente.HasValue)
            acenteKodu.DisAcente = request.DisAcente.Value;

        var userId = int.TryParse(_currentUserService.UserId, out var parsedUserId) ? parsedUserId : 0;
        acenteKodu.GuncelleyenUyeId = userId;
        acenteKodu.GuncellenmeTarihi = _dateTimeService.Now;

        await _context.SaveChangesAsync(cancellationToken);

        return acenteKodu;
    }
}
