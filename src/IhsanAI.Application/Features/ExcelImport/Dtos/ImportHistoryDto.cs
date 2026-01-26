namespace IhsanAI.Application.Features.ExcelImport.Dtos;

public record ImportHistoryDto
{
    public int Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public int SigortaSirketiId { get; init; }
    public string SigortaSirketiAdi { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public string Status { get; init; } = string.Empty;  // Completed, Failed, Partial
    public DateTime ImportDate { get; init; }
    public string? ImportedBy { get; init; }
}

public record ImportHistoryListDto
{
    public List<ImportHistoryDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
}
