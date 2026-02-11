using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Exceptions;

namespace IhsanAI.Application.Features.Musteriler.Commands;

/// <summary>
/// İki müşteri kaydını birleştirir. PrimaryMusteriId hayatta kalır, SecondaryMusteriId silinir.
/// Tüm poliçe ve havuz referansları Primary'ye taşınır.
/// </summary>
public record MergeCustomersCommand(int PrimaryMusteriId, int SecondaryMusteriId) : IRequest<MergeCustomersResult>;

public record MergeCustomersResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int PoliciesUpdated { get; init; }
    public int HavuzUpdated { get; init; }
    public int YakalananUpdated { get; init; }
}

public class MergeCustomersCommandHandler : IRequestHandler<MergeCustomersCommand, MergeCustomersResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public MergeCustomersCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<MergeCustomersResult> Handle(MergeCustomersCommand request, CancellationToken cancellationToken)
    {
        if (request.PrimaryMusteriId == request.SecondaryMusteriId)
        {
            return new MergeCustomersResult { Success = false, ErrorMessage = "Aynı müşteri kendisiyle birleştirilemez." };
        }

        var primary = await _context.Musteriler
            .FirstOrDefaultAsync(m => m.Id == request.PrimaryMusteriId, cancellationToken);
        var secondary = await _context.Musteriler
            .FirstOrDefaultAsync(m => m.Id == request.SecondaryMusteriId, cancellationToken);

        if (primary == null)
            return new MergeCustomersResult { Success = false, ErrorMessage = "Birincil müşteri bulunamadı." };
        if (secondary == null)
            return new MergeCustomersResult { Success = false, ErrorMessage = "İkincil müşteri bulunamadı." };

        // Firma doğrulaması
        if (_currentUserService.FirmaId.HasValue &&
            (primary.EkleyenFirmaId != _currentUserService.FirmaId || secondary.EkleyenFirmaId != _currentUserService.FirmaId))
        {
            throw new ForbiddenAccessException("Bu müşterileri birleştirme yetkiniz yok.");
        }

        // Secondary'den boş olan alanları Primary'ye kopyala
        if (string.IsNullOrWhiteSpace(primary.TcKimlikNo) && !string.IsNullOrWhiteSpace(secondary.TcKimlikNo))
            primary.TcKimlikNo = secondary.TcKimlikNo;
        if (string.IsNullOrWhiteSpace(primary.VergiNo) && !string.IsNullOrWhiteSpace(secondary.VergiNo))
            primary.VergiNo = secondary.VergiNo;
        if (string.IsNullOrWhiteSpace(primary.TcVergiNo) && !string.IsNullOrWhiteSpace(secondary.TcVergiNo))
            primary.TcVergiNo = secondary.TcVergiNo;
        if (string.IsNullOrWhiteSpace(primary.Gsm) && !string.IsNullOrWhiteSpace(secondary.Gsm))
            primary.Gsm = secondary.Gsm;
        if (string.IsNullOrWhiteSpace(primary.Email) && !string.IsNullOrWhiteSpace(secondary.Email))
            primary.Email = secondary.Email;
        if (!primary.DogumTarihi.HasValue && secondary.DogumTarihi.HasValue)
            primary.DogumTarihi = secondary.DogumTarihi;
        if (string.IsNullOrWhiteSpace(primary.DogumYeri) && !string.IsNullOrWhiteSpace(secondary.DogumYeri))
            primary.DogumYeri = secondary.DogumYeri;

        primary.GuncellenmeZamani = _dateTimeService.Now;
        primary.GuncelleyenUyeId = int.TryParse(_currentUserService.UserId, out var uid) ? uid : null;

        // FK referanslarını güncelle: Policeler
        var policeler = await _context.Policeler
            .Where(p => p.MusteriId == request.SecondaryMusteriId)
            .ToListAsync(cancellationToken);
        foreach (var p in policeler) p.MusteriId = request.PrimaryMusteriId;

        // FK referanslarını güncelle: PoliceHavuzlari
        var havuz = await _context.PoliceHavuzlari
            .Where(p => p.MusteriId == request.SecondaryMusteriId)
            .ToListAsync(cancellationToken);
        foreach (var p in havuz) p.MusteriId = request.PrimaryMusteriId;

        var havuzEttiren = await _context.PoliceHavuzlari
            .Where(p => p.SigortaEttirenId == request.SecondaryMusteriId)
            .ToListAsync(cancellationToken);
        foreach (var p in havuzEttiren) p.SigortaEttirenId = request.PrimaryMusteriId;

        // FK referanslarını güncelle: YakalananPoliceler
        var yakalanan = await _context.YakalananPoliceler
            .Where(p => p.MusteriId == request.SecondaryMusteriId)
            .ToListAsync(cancellationToken);
        foreach (var p in yakalanan) p.MusteriId = request.PrimaryMusteriId;

        // Secondary müşteriyi sil
        _context.Musteriler.Remove(secondary);

        await _context.SaveChangesAsync(cancellationToken);

        return new MergeCustomersResult
        {
            Success = true,
            PoliciesUpdated = policeler.Count,
            HavuzUpdated = havuz.Count + havuzEttiren.Count,
            YakalananUpdated = yakalanan.Count
        };
    }
}
