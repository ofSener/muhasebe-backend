using IhsanAI.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IhsanAI.Infrastructure.Services;

/// <summary>
/// Acentelik kontrolü servisi implementasyonu
/// </summary>
public class AcentelikService : IAcentelikService
{
    private readonly IApplicationDbContext _context;

    public AcentelikService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Verilen sigorta şirketi ve acente bilgilerine göre firma acenteliklerinde kontrol yapar
    /// </summary>
    public async Task<bool> AcentelikVarMi(int firmaId, int sigortaSirketiId, string? acenteNo, string? acenteAdi)
    {
        // Boş değerler için normalize et
        var normalizedAcenteNo = (acenteNo ?? string.Empty).Replace(" ", string.Empty).ToLower();
        var normalizedAcenteAdi = (acenteAdi ?? string.Empty).Replace(" ", string.Empty).ToLower();

        // Firma acenteliklerinde ara
        var acentelikVar = await _context.AcenteKodlari
            .Where(a => a.FirmaId == firmaId
                     && a.SigortaSirketiId == sigortaSirketiId
                     && (
                         // Acente numarası ile eşleşme
                         (normalizedAcenteNo != string.Empty && a.AcenteKoduDeger.ToLower().Contains(normalizedAcenteNo))
                         ||
                         // Acente adı ile eşleşme
                         (normalizedAcenteAdi != string.Empty && a.AcenteAdi.ToLower().Contains(normalizedAcenteAdi))
                     ))
            .AnyAsync();

        return acentelikVar;
    }
}
