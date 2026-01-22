using System.Text.Json.Serialization;
using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.AcenteKodlari.Commands;

public record CreateAcenteKoduCommand(
    [property: JsonPropertyName("sigortaSirketiId")] int SigortaSirketiId,
    [property: JsonPropertyName("acenteKoduDeger")] string AcenteKoduDeger,
    [property: JsonPropertyName("acenteAdi")] string AcenteAdi,
    [property: JsonPropertyName("firmaId")] int FirmaId,
    [property: JsonPropertyName("disAcente")] sbyte DisAcente = 0
) : IRequest<AcenteKodu>;

public class CreateAcenteKoduCommandHandler : IRequestHandler<CreateAcenteKoduCommand, AcenteKodu>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICurrentUserService _currentUserService;

    public CreateAcenteKoduCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _currentUserService = currentUserService;
    }

    public async Task<AcenteKodu> Handle(CreateAcenteKoduCommand request, CancellationToken cancellationToken)
    {
        // Firma doğrulaması: Kullanıcı sadece kendi firmasının acente kodunu oluşturabilir
        if (_currentUserService.FirmaId.HasValue && request.FirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu firma için acente kodu oluşturma yetkiniz yok.");
        }

        var userId = int.TryParse(_currentUserService.UserId, out var parsedUserId) ? parsedUserId : 0;

        var acenteKodu = new AcenteKodu
        {
            SigortaSirketiId = request.SigortaSirketiId,
            AcenteKoduDeger = request.AcenteKoduDeger,
            AcenteAdi = request.AcenteAdi,
            FirmaId = request.FirmaId,
            DisAcente = request.DisAcente,
            UyeId = userId,
            GuncelleyenUyeId = userId,
            EklenmeTarihi = _dateTimeService.Now,
            GuncellenmeTarihi = _dateTimeService.Now,
            OtomatikEklendi = "0"
        };

        _context.AcenteKodlari.Add(acenteKodu);
        await _context.SaveChangesAsync(cancellationToken);

        return acenteKodu;
    }
}
