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
    private readonly ICurrentUserService _currentUserService;

    public CreateCustomerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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
            // GÜVENLİK: Token'dan otomatik al, client'a güvenme!
            EkleyenFirmaId = _currentUserService.FirmaId,
            EkleyenUyeId = _currentUserService.UyeId,
            EkleyenSubeId = _currentUserService.SubeId,
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
