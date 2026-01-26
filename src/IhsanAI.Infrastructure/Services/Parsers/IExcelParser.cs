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
    /// Bu parser'ın dosyayı parse edip edemeyeceğini kontrol eder
    /// </summary>
    bool CanParse(string fileName, IEnumerable<string> headerColumns);

    /// <summary>
    /// Excel satırlarını parse eder
    /// </summary>
    List<ExcelImportRowDto> Parse(IEnumerable<IDictionary<string, object?>> rows);
}
