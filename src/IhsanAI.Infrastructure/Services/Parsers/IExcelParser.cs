using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Infrastructure.Services.Parsers;

public interface IExcelParser
{
    /// <summary>
    /// Sigorta şirketi ID'si
    /// </summary>
    int SigortaSirketiId { get; }

    /// <summary>
    /// Şirket adı (tanımlama için)
    /// </summary>
    string SirketAdi { get; }

    /// <summary>
    /// Dosya adındaki pattern'ler (otomatik tespit için)
    /// </summary>
    string[] FileNamePatterns { get; }

    /// <summary>
    /// Header satır numarası (1-indexed). Null ise otomatik tespit edilir.
    /// </summary>
    int? HeaderRowIndex { get; }

    /// <summary>
    /// Ana sayfa adı (null ise ilk sayfayı kullanır)
    /// </summary>
    string? MainSheetName => null;

    /// <summary>
    /// Ek olarak okunması gereken sayfa isimleri (ör: Sigortalilar)
    /// </summary>
    string[]? AdditionalSheetNames => null;

    /// <summary>
    /// Bu parser'ın dosyayı parse edip edemeyeceğini kontrol eder
    /// </summary>
    bool CanParse(string fileName, IEnumerable<string> headerColumns);

    /// <summary>
    /// Excel satırlarını parse eder
    /// </summary>
    List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows);

    /// <summary>
    /// Ek sayfa verilerini kullanarak parse eder (ör: Sigortalilar sayfasından TC/Ad/Soyad)
    /// </summary>
    List<ExcelImportRowDto> ParseWithAdditionalSheets(
        IEnumerable<IDictionary<string, object?>> mainRows,
        Dictionary<string, List<IDictionary<string, object?>>> additionalSheets)
        => Parse(mainRows);  // Default: sadece ana sayfayı parse et
}
