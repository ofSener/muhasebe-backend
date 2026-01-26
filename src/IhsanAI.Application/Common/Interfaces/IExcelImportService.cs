using IhsanAI.Application.Features.ExcelImport.Dtos;

namespace IhsanAI.Application.Common.Interfaces;

public interface IExcelImportService
{
    /// <summary>
    /// Excel dosyasını parse eder ve önizleme verisi döner
    /// </summary>
    Task<ExcelImportPreviewDto> ParseExcelAsync(Stream fileStream, string fileName, int? sigortaSirketiId = null);

    /// <summary>
    /// Parse edilmiş verileri PoliceHavuz tablosuna kaydeder
    /// </summary>
    Task<ExcelImportResultDto> ConfirmImportAsync(string sessionId);

    /// <summary>
    /// Desteklenen formatları listeler
    /// </summary>
    Task<SupportedFormatsResponseDto> GetSupportedFormatsAsync();

    /// <summary>
    /// Import geçmişini listeler
    /// </summary>
    Task<ImportHistoryListDto> GetImportHistoryAsync(int? firmaId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Dosya adından sigorta şirketini tespit eder
    /// </summary>
    int? DetectSigortaSirketiFromFileName(string fileName);
}
