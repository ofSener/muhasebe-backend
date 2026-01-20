using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.Policeler.Commands;

public record SendPolicyToPoolCommand(int YakalananPoliceId) : IRequest<SendPolicyToPoolResultDto>;

public record SendPolicyToPoolResultDto
{
    public bool Success { get; init; }
    public int? PoliceHavuzId { get; init; }
    public string? ErrorMessage { get; init; }
}

public class SendPolicyToPoolCommandHandler : IRequestHandler<SendPolicyToPoolCommand, SendPolicyToPoolResultDto>
{
    private readonly IApplicationDbContext _context;

    public SendPolicyToPoolCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SendPolicyToPoolResultDto> Handle(SendPolicyToPoolCommand request, CancellationToken cancellationToken)
    {
        var yakalanan = await _context.YakalananPoliceler
            .FirstOrDefaultAsync(y => y.Id == request.YakalananPoliceId, cancellationToken);

        if (yakalanan == null)
        {
            return new SendPolicyToPoolResultDto
            {
                Success = false,
                ErrorMessage = "Yakalanan poliçe bulunamadı"
            };
        }

        var policeHavuz = new PoliceHavuz
        {
            PoliceTipi = yakalanan.PoliceTuru.ToString(),
            PoliceNo = yakalanan.PoliceNumarasi,
            Plaka = yakalanan.Plaka,
            ZeyilNo = 0,
            SigortaSirketiId = yakalanan.SigortaSirketi,
            TanzimTarihi = yakalanan.TanzimTarihi,
            BaslangicTarihi = yakalanan.BaslangicTarihi,
            BitisTarihi = yakalanan.BitisTarihi,
            SigortaEttirenId = yakalanan.MusteriId ?? 0,
            BrutPrim = (decimal)yakalanan.BrutPrim,
            NetPrim = (decimal)yakalanan.NetPrim,
            Vergi = 0,
            Komisyon = 0,
            BransId = yakalanan.PoliceTuru,
            DisPolice = yakalanan.DisPolice ?? 0,
            MusteriId = yakalanan.MusteriId ?? 0,
            PoliceTespitKaynakId = 0,
            IsOrtagiFirmaId = yakalanan.FirmaId,
            IsOrtagiSubeId = yakalanan.ProduktorSubeId,
            IsOrtagiUyeId = yakalanan.ProduktorId,
            IsOrtagiKomisyonOrani = 0,
            IsOrtagiKomisyon = 0,
            IsOrtagiEslestirmeKriteri = 0,
            KaynakDosyaId = 0,
            KayitDurumu = 1,
            EklenmeTarihi = DateTime.UtcNow,
            GuncelleyenKullaniciId = yakalanan.UyeId,
            Aciklama = yakalanan.Aciklama
        };

        _context.PoliceHavuzlari.Add(policeHavuz);
        await _context.SaveChangesAsync(cancellationToken);

        return new SendPolicyToPoolResultDto
        {
            Success = true,
            PoliceHavuzId = policeHavuz.Id
        };
    }
}
