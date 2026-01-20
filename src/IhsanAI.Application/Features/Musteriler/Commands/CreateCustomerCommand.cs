using MediatR;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Musteriler.Commands;

public record CreateCustomerCommand(
    sbyte? SahipTuru,
    string? TcKimlikNo,
    string? VergiNo,
    string? TcVergiNo,
    string? Adi,
    string? Soyadi,
    string? DogumYeri,
    DateTime? DogumTarihi,
    string? Cinsiyet,
    string? BabaAdi,
    string? Gsm,
    string? Gsm2,
    string? Telefon,
    string? Email,
    string? Meslek,
    string? YasadigiIl,
    string? YasadigiIlce,
    int? Boy,
    int? Kilo,
    int? EkleyenFirmaId,
    int? EkleyenUyeId,
    int? EkleyenSubeId
) : IRequest<CreateCustomerResultDto>;

public record CreateCustomerResultDto
{
    public bool Success { get; init; }
    public int? CustomerId { get; init; }
    public string? ErrorMessage { get; init; }
}

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CreateCustomerResultDto>
{
    private readonly IApplicationDbContext _context;

    public CreateCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CreateCustomerResultDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var musteri = new Musteri
        {
            SahipTuru = request.SahipTuru,
            TcKimlikNo = request.TcKimlikNo,
            VergiNo = request.VergiNo,
            TcVergiNo = request.TcVergiNo,
            Adi = request.Adi,
            Soyadi = request.Soyadi,
            DogumYeri = request.DogumYeri,
            DogumTarihi = request.DogumTarihi,
            Cinsiyet = request.Cinsiyet,
            BabaAdi = request.BabaAdi,
            Gsm = request.Gsm,
            Gsm2 = request.Gsm2,
            Telefon = request.Telefon,
            Email = request.Email,
            Meslek = request.Meslek,
            YasadigiIl = request.YasadigiIl,
            YasadigiIlce = request.YasadigiIlce,
            Boy = request.Boy,
            Kilo = request.Kilo,
            EkleyenFirmaId = request.EkleyenFirmaId,
            EkleyenUyeId = request.EkleyenUyeId,
            EkleyenSubeId = request.EkleyenSubeId,
            EklenmeZamani = DateTime.UtcNow
        };

        _context.Musteriler.Add(musteri);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateCustomerResultDto
        {
            Success = true,
            CustomerId = musteri.Id
        };
    }
}
