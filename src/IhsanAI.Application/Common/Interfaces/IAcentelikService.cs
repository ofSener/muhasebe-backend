namespace IhsanAI.Application.Common.Interfaces;

/// <summary>
/// Acentelik kontrolü servisi
/// </summary>
public interface IAcentelikService
{
    /// <summary>
    /// Verilen sigorta şirketi ve acente bilgilerine göre firma acenteliklerinde kontrol yapar
    /// </summary>
    /// <param name="firmaId">Firma ID</param>
    /// <param name="sigortaSirketiId">Sigorta şirketi ID</param>
    /// <param name="acenteNo">Acente numarası</param>
    /// <param name="acenteAdi">Acente adı (kısaltılmış)</param>
    /// <returns>Acentelik bulundu mu?</returns>
    Task<bool> AcentelikVarMi(int firmaId, int sigortaSirketiId, string? acenteNo, string? acenteAdi);
}
