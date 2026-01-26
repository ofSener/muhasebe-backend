namespace IhsanAI.Application.Features.ExcelImport.Dtos;

public record ExcelImportPreviewDto
{
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int InvalidRows { get; init; }
    public List<ExcelImportRowDto> Rows { get; init; } = new();
    public string ImportSessionId { get; init; } = string.Empty;  // For confirmation
    public string FileName { get; init; } = string.Empty;
    public int SigortaSirketiId { get; init; }
    public string SigortaSirketiAdi { get; init; } = string.Empty;
    public string? DetectedFormat { get; init; }  // Auto-detected company format
}
