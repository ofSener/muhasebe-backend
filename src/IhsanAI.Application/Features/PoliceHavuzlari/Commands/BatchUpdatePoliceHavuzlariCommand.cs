using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Policeler.Commands;

namespace IhsanAI.Application.Features.PoliceHavuzlari.Commands;

public record PoliceHavuzChangesDto
{
    public string? PolicyNo { get; init; }
    public int? Customer { get; init; }
    public string? Type { get; init; }
    public decimal? NetPremium { get; init; }
    public decimal? GrossPremium { get; init; }
    public string? Producer { get; init; }
    public string? Branch { get; init; }
    public string? Date { get; init; }
}

public record PoliceHavuzUpdateDto
{
    public int PolicyId { get; init; }
    public PoliceHavuzChangesDto? Changes { get; init; }
}

public record BatchUpdatePoliceHavuzlariCommand(
    List<PoliceHavuzUpdateDto> Updates
) : IRequest<BatchUpdateResultDto>;

public class BatchUpdatePoliceHavuzlariCommandHandler
    : IRequestHandler<BatchUpdatePoliceHavuzlariCommand, BatchUpdateResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BatchUpdatePoliceHavuzlariCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<BatchUpdateResultDto> Handle(
        BatchUpdatePoliceHavuzlariCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Updates == null || request.Updates.Count == 0)
        {
            return new BatchUpdateResultDto
            {
                UpdatedCount = 0,
                FailedCount = 0,
                FailedIds = new List<int>()
            };
        }

        // Lookup dictionary'leri hazırla (aynı isimde birden fazla kullanıcı olabilir, ilkini al)
        var kullanicilarList = await _context.Kullanicilar
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var kullanicilar = kullanicilarList
            .GroupBy(k => $"{k.Adi} {k.Soyadi}".Trim())
            .ToDictionary(g => g.Key, g => g.First().Id);

        var subelerList = await _context.Subeler
            .AsNoTracking()
            .Where(s => s.SubeAdi != null)
            .ToListAsync(cancellationToken);
        var subeler = subelerList
            .Where(s => !string.IsNullOrEmpty(s.SubeAdi))
            .ToDictionary(s => s.SubeAdi!, s => s.Id);

        var policeTurleriList = await _context.PoliceTurleri
            .AsNoTracking()
            .Where(p => p.Turu != null)
            .ToListAsync(cancellationToken);
        var policeTurleri = policeTurleriList
            .Where(p => !string.IsNullOrEmpty(p.Turu))
            .ToDictionary(p => p.Turu!, p => p.Id);

        // Güncellenecek kayıtları çek
        var ids = request.Updates.Select(u => u.PolicyId).ToList();
        var policies = await _context.PoliceHavuzlari
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var updatedCount = 0;
        var failedIds = new List<int>();

        // Her güncellemeyi uygula
        foreach (var update in request.Updates)
        {
            var policy = policies.FirstOrDefault(p => p.Id == update.PolicyId);
            if (policy == null)
            {
                failedIds.Add(update.PolicyId);
                continue;
            }

            if (update.Changes == null)
            {
                continue;
            }

            var changes = update.Changes;

            // Producer (isim -> ID) - PoliceHavuz'da IsOrtagiUyeId kullanılıyor
            if (!string.IsNullOrEmpty(changes.Producer))
            {
                if (kullanicilar.TryGetValue(changes.Producer, out var produktorId))
                {
                    policy.IsOrtagiUyeId = produktorId;
                }
            }

            // Branch (isim -> ID) - PoliceHavuz'da IsOrtagiSubeId kullanılıyor
            if (!string.IsNullOrEmpty(changes.Branch))
            {
                if (subeler.TryGetValue(changes.Branch, out var subeId))
                {
                    policy.IsOrtagiSubeId = subeId;
                }
            }

            // Type (isim -> ID) - PoliceHavuz'da BransId kullanılıyor
            if (!string.IsNullOrEmpty(changes.Type))
            {
                if (policeTurleri.TryGetValue(changes.Type, out var turId))
                {
                    policy.BransId = turId;
                }
            }

            // NetPremium
            if (changes.NetPremium.HasValue)
            {
                policy.NetPrim = changes.NetPremium.Value;
            }

            // GrossPremium
            if (changes.GrossPremium.HasValue)
            {
                policy.BrutPrim = changes.GrossPremium.Value;
            }

            // Date (TanzimTarihi)
            if (!string.IsNullOrEmpty(changes.Date))
            {
                if (DateTime.TryParse(changes.Date, out var date))
                {
                    policy.TanzimTarihi = date;
                }
            }

            // PolicyNo (MaxLength: 50)
            if (!string.IsNullOrEmpty(changes.PolicyNo))
            {
                if (changes.PolicyNo.Length <= 50)
                {
                    policy.PoliceNo = changes.PolicyNo;
                }
            }

            // Customer (MusteriId)
            if (changes.Customer.HasValue)
            {
                policy.MusteriId = changes.Customer.Value;
            }

            // Audit alanları
            if (int.TryParse(_currentUserService.UserId, out var userId))
            {
                policy.GuncelleyenKullaniciId = userId;
            }
            policy.GuncellenmeTarihi = DateTime.UtcNow;

            updatedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new BatchUpdateResultDto
        {
            UpdatedCount = updatedCount,
            FailedCount = failedIds.Count,
            FailedIds = failedIds
        };
    }
}
