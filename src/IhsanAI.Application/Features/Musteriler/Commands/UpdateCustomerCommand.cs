using System.Text.Json.Serialization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Musteriler.Commands;

public record UpdateCustomerCommand(
    int Id,
    [property: JsonPropertyName("sahipTuru")] sbyte? SahipTuru = null,
    [property: JsonPropertyName("tcKimlikNo")] string? TcKimlikNo = null,
    [property: JsonPropertyName("vergiNo")] string? VergiNo = null,
    [property: JsonPropertyName("tcVergiNo")] string? TcVergiNo = null,
    [property: JsonPropertyName("adi")] string? Adi = null,
    [property: JsonPropertyName("soyadi")] string? Soyadi = null,
    [property: JsonPropertyName("dogumYeri")] string? DogumYeri = null,
    [property: JsonPropertyName("dogumTarihi")] DateTime? DogumTarihi = null,
    [property: JsonPropertyName("cinsiyet")] string? Cinsiyet = null,
    [property: JsonPropertyName("babaAdi")] string? BabaAdi = null,
    [property: JsonPropertyName("gsm")] string? Gsm = null,
    [property: JsonPropertyName("gsm2")] string? Gsm2 = null,
    [property: JsonPropertyName("telefon")] string? Telefon = null,
    [property: JsonPropertyName("email")] string? Email = null,
    [property: JsonPropertyName("meslek")] string? Meslek = null,
    [property: JsonPropertyName("yasadigiIl")] string? YasadigiIl = null,
    [property: JsonPropertyName("yasadigiIlce")] string? YasadigiIlce = null,
    [property: JsonPropertyName("boy")] int? Boy = null,
    [property: JsonPropertyName("kilo")] int? Kilo = null
) : IRequest<UpdateCustomerResultDto>;

public record UpdateCustomerResultDto
{
    public bool Success { get; init; }
    public int? CustomerId { get; init; }
    public string? ErrorMessage { get; init; }
}

public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, UpdateCustomerResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCustomerCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _currentUserService = currentUserService;
    }

    public async Task<UpdateCustomerResultDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var musteri = await _context.Musteriler
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (musteri == null)
        {
            return new UpdateCustomerResultDto
            {
                Success = false,
                ErrorMessage = "Müşteri bulunamadı."
            };
        }

        // Firma doğrulaması: Kullanıcı sadece kendi firmasının müşterisini güncelleyebilir
        if (_currentUserService.FirmaId.HasValue && musteri.EkleyenFirmaId != _currentUserService.FirmaId.Value)
        {
            throw new ForbiddenAccessException("Bu müşteriyi güncelleme yetkiniz yok.");
        }

        // Update fields if provided
        if (request.SahipTuru.HasValue)
            musteri.SahipTuru = request.SahipTuru.Value;

        if (request.TcKimlikNo != null)
            musteri.TcKimlikNo = request.TcKimlikNo;

        if (request.VergiNo != null)
            musteri.VergiNo = request.VergiNo;

        if (request.TcVergiNo != null)
            musteri.TcVergiNo = request.TcVergiNo;

        if (request.Adi != null)
            musteri.Adi = request.Adi;

        if (request.Soyadi != null)
            musteri.Soyadi = request.Soyadi;

        if (request.DogumYeri != null)
            musteri.DogumYeri = request.DogumYeri;

        if (request.DogumTarihi.HasValue)
            musteri.DogumTarihi = request.DogumTarihi.Value;

        if (request.Cinsiyet != null)
            musteri.Cinsiyet = request.Cinsiyet;

        if (request.BabaAdi != null)
            musteri.BabaAdi = request.BabaAdi;

        if (request.Gsm != null)
            musteri.Gsm = request.Gsm;

        if (request.Gsm2 != null)
            musteri.Gsm2 = request.Gsm2;

        if (request.Telefon != null)
            musteri.Telefon = request.Telefon;

        if (request.Email != null)
            musteri.Email = request.Email;

        if (request.Meslek != null)
            musteri.Meslek = request.Meslek;

        if (request.YasadigiIl != null)
            musteri.YasadigiIl = request.YasadigiIl;

        if (request.YasadigiIlce != null)
            musteri.YasadigiIlce = request.YasadigiIlce;

        if (request.Boy.HasValue)
            musteri.Boy = request.Boy.Value;

        if (request.Kilo.HasValue)
            musteri.Kilo = request.Kilo.Value;

        // Update tracking fields
        var userId = int.TryParse(_currentUserService.UserId, out var parsedUserId) ? parsedUserId : 0;
        musteri.GuncelleyenUyeId = userId;
        musteri.GuncellenmeZamani = _dateTimeService.Now;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateCustomerResultDto
        {
            Success = true,
            CustomerId = musteri.Id
        };
    }
}
