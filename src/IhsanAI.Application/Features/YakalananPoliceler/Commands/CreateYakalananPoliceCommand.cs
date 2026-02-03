using MediatR;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.YakalananPoliceler.Commands;

/// <summary>
/// Yakalanan poliçe oluşturma komutu
/// </summary>
public record CreateYakalananPoliceCommand : IRequest<CreateYakalananPoliceResult>
{
    public int SigortaSirketi { get; init; }
    public int PoliceTuru { get; init; }
    public string PoliceNumarasi { get; init; } = string.Empty;
    public string Plaka { get; init; } = string.Empty;
    public DateTime TanzimTarihi { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public float BrutPrim { get; init; }
    public float NetPrim { get; init; }
    public string? SigortaliAdi { get; init; }
    public int? MusteriId { get; init; }
    public int? CepTelefonu { get; init; }
    public sbyte? DisPolice { get; init; }
    public string? AcenteAdi { get; init; }
    public string? AcenteNo { get; init; }
    public string? Aciklama { get; init; }
}

public record CreateYakalananPoliceResult
{
    public bool Success { get; init; }
    public int? Id { get; init; }
    public string? Error { get; init; }
}

public class CreateYakalananPoliceCommandHandler : IRequestHandler<CreateYakalananPoliceCommand, CreateYakalananPoliceResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public CreateYakalananPoliceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<CreateYakalananPoliceResult> Handle(CreateYakalananPoliceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entity = new Domain.Entities.YakalananPolice
            {
                SigortaSirketi = request.SigortaSirketi,
                PoliceTuru = request.PoliceTuru,
                PoliceNumarasi = request.PoliceNumarasi,
                Plaka = request.Plaka ?? string.Empty,
                TanzimTarihi = request.TanzimTarihi,
                BaslangicTarihi = request.BaslangicTarihi,
                BitisTarihi = request.BitisTarihi,
                BrutPrim = request.BrutPrim,
                NetPrim = request.NetPrim,
                SigortaliAdi = request.SigortaliAdi,
                ProduktorId = 0,
                ProduktorSubeId = 0,
                UyeId = _currentUserService.UyeId ?? 0,
                SubeId = _currentUserService.SubeId ?? 0,
                FirmaId = _currentUserService.FirmaId ?? 0,
                MusteriId = request.MusteriId,
                CepTelefonu = request.CepTelefonu,
                GuncelleyenUyeId = null,
                DisPolice = request.DisPolice ?? 0,
                AcenteAdi = request.AcenteAdi,
                AcenteNo = request.AcenteNo ?? string.Empty,
                EklenmeTarihi = _dateTimeService.Now,
                GuncellenmeTarihi = null,
                Aciklama = request.Aciklama,
                IsDeleted = 0
            };

            _context.YakalananPoliceler.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return new CreateYakalananPoliceResult
            {
                Success = true,
                Id = entity.Id
            };
        }
        catch (Exception ex)
        {
            return new CreateYakalananPoliceResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }
}
